using FluentValidation;
using global::ECommerceGRPC.ProductService;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProductService.gRPC.Data;
using ProductService.gRPC.Mappers;
using ProductService.gRPC.Models;

namespace ProductService.gRPC.Services
{
    /// <summary>
    /// Implementación del servicio gRPC para gestión de productos
    /// </summary>
    public class ProductGrpcService : global::ECommerceGRPC.ProductService.ProductService.ProductServiceBase
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<ProductGrpcService> _logger;
        private readonly IValidator<CreateProductRequest> _createValidator;
        private readonly IValidator<UpdateProductRequest> _updateValidator;
        private readonly IValidator<GetProductRequest> _getValidator;
        private readonly IValidator<GetProductsRequest> _getProductsValidator;
        private readonly IValidator<SearchProductsRequest> _searchValidator;
        private readonly IValidator<DeleteProductRequest> _deleteValidator;

        public ProductGrpcService(
            ProductDbContext context,
            ILogger<ProductGrpcService> logger,
            IValidator<CreateProductRequest> createValidator,
            IValidator<UpdateProductRequest> updateValidator,
            IValidator<GetProductRequest> getValidator,
            IValidator<GetProductsRequest> getProductsValidator,
            IValidator<SearchProductsRequest> searchValidator,
            IValidator<DeleteProductRequest> deleteValidator)
        {
            _context = context;
            _logger = logger;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _getValidator = getValidator;
            _getProductsValidator = getProductsValidator;
            _searchValidator = searchValidator;
            _deleteValidator = deleteValidator;
        }

        /// <summary>
        /// Obtiene un producto específico por ID (Unary)
        /// </summary>
        public override async Task<ProductResponse> GetProduct(
            GetProductRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"GetProduct llamado para ID: {request.Id}");

                // Validar solicitud
                var validationResult = await _getValidator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Validación fallida: {errors}");
                    throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
                }

                // Buscar producto en la base de datos
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == request.Id);

                if (product == null)
                {
                    _logger.LogWarning($"Producto con ID {request.Id} no encontrado");
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Producto con ID {request.Id} no existe"));
                }

                _logger.LogInformation($"Producto {product.Name} encontrado exitosamente");
                return MapToProductResponse(product);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener producto con ID {request.Id}");
                throw new RpcException(new Status(StatusCode.Internal,
                    "Error interno al procesar la solicitud"));
            }
        }

        /// <summary>
        /// Lista productos con paginación y filtros (Server Streaming)
        /// </summary>
        public override async Task GetProducts(
            GetProductsRequest request,
            IServerStreamWriter<ProductResponse> responseStream,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("GetProducts llamado con paginación: " +
                    $"Página={request.PageNumber}, Tamaño={request.PageSize}");

                // Validar solicitud
                var validationResult = await _getProductsValidator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Validación fallida: {errors}");
                    throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
                }

                // Construir query base
                var query = _context.Products.AsQueryable();

                // Aplicar filtro de activos si se solicita
                if (request.ActiveOnly)
                {
                    query = query.Where(p => p.IsActive);
                }

                // Aplicar filtro de categoría si se proporciona
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    query = query.Where(p => p.Category == request.Category);
                }

                // Aplicar filtro de precio mínimo si se proporciona
                if (request.MinPrice > 0)
                {
                    var minPrice = Convert.ToDecimal(request.MinPrice);
                    query = query.Where(p => p.Price >= minPrice);
                }

                // Aplicar filtro de precio máximo si se proporciona
                if (request.MaxPrice > 0)
                {
                    var maxPrice = Convert.ToDecimal(request.MaxPrice);
                    query = query.Where(p => p.Price <= maxPrice);
                }

                // Aplicar ordenamiento
                query = query.OrderBy(p => p.Name);

                // Calcular paginación
                var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
                var pageSize = request.PageSize > 0 ? request.PageSize : 10;
                var skip = (pageNumber - 1) * pageSize;

                // Obtener productos con paginación
                var products = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation($"Se encontraron {products.Count} productos");

                // Enviar productos en streaming
                foreach (var product in products)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("GetProducts cancelado por el cliente");
                        break;
                    }

                    var response = MapToProductResponse(product);
                    await responseStream.WriteAsync(response);
                }

                _logger.LogInformation($"GetProducts completado - {products.Count} productos enviados");
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lista de productos");
                throw new RpcException(new Status(StatusCode.Internal,
                    "Error interno al procesar la solicitud"));
            }
        }

        /// <summary>
        /// Crea un nuevo producto (Unary)
        /// </summary>
        public override async Task<ProductResponse> CreateProduct(
            CreateProductRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"CreateProduct llamado para: {request.Name}");

                // Validar solicitud
                var validationResult = await _createValidator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Validación fallida: {errors}");
                    throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
                }

                // Verificar si ya existe un producto con el mismo nombre
                var existingProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Name == request.Name);

                if (existingProduct != null)
                {
                    _logger.LogWarning($"Producto con nombre '{request.Name}' ya existe");
                    throw new RpcException(new Status(StatusCode.AlreadyExists,
                        $"Ya existe un producto con el nombre '{request.Name}'"));
                }

                // Crear nueva entidad
                var product = new Product
                {
                    Name = request.Name,
                    Description = request.Description,
                    Price = Convert.ToDecimal(request.Price),
                    Stock = request.Stock,
                    Category = request.Category,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                // Guardar en la base de datos
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Producto creado exitosamente con ID: {product.Id}");
                return MapToProductResponse(product);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto");
                throw new RpcException(new Status(StatusCode.Internal,
                    "Error interno al procesar la solicitud"));
            }
        }

        /// <summary>
        /// Actualiza un producto existente (Unary)
        /// </summary>
        public override async Task<ProductResponse> UpdateProduct(
            UpdateProductRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"UpdateProduct llamado para ID: {request.Id}");

                // Validar solicitud
                var validationResult = await _updateValidator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Validación fallida: {errors}");
                    throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
                }

                // Buscar producto existente
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == request.Id);

                if (product == null)
                {
                    _logger.LogWarning($"Producto con ID {request.Id} no encontrado");
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Producto con ID {request.Id} no existe"));
                }

                // Verificar si el nuevo nombre ya existe en otro producto
                if (product.Name != request.Name)
                {
                    var existingProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.Name == request.Name && p.Id != request.Id);

                    if (existingProduct != null)
                    {
                        _logger.LogWarning($"Producto con nombre '{request.Name}' ya existe");
                        throw new RpcException(new Status(StatusCode.AlreadyExists,
                            $"Ya existe otro producto con el nombre '{request.Name}'"));
                    }
                }

                // Actualizar propiedades
                product.Name = request.Name;
                product.Description = request.Description;
                product.Price = Convert.ToDecimal(request.Price);
                product.Stock = request.Stock;
                product.Category = request.Category;
                product.IsActive = request.IsActive;
                product.UpdatedAt = DateTime.UtcNow;

                // Guardar cambios
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Producto {product.Id} actualizado exitosamente");
                return MapToProductResponse(product);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al actualizar producto con ID {request.Id}");
                throw new RpcException(new Status(StatusCode.Internal,
                    "Error interno al procesar la solicitud"));
            }
        }

        /// <summary>
        /// Elimina un producto de forma lógica (Unary)
        /// </summary>
        public override async Task<DeleteProductResponse> DeleteProduct(
            DeleteProductRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"DeleteProduct llamado para ID: {request.Id}");

                // Validar solicitud
                var validationResult = await _deleteValidator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Validación fallida: {errors}");
                    throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
                }

                // Buscar producto
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == request.Id);

                if (product == null)
                {
                    _logger.LogWarning($"Producto con ID {request.Id} no encontrado");
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Producto con ID {request.Id} no existe"));
                }

                // Eliminación lógica
                product.IsActive = false;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Producto {product.Id} eliminado exitosamente (lógico)");

                return new DeleteProductResponse
                {
                    Success = true,
                    Message = $"Producto '{product.Name}' eliminado exitosamente"
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al eliminar producto con ID {request.Id}");
                throw new RpcException(new Status(StatusCode.Internal,
                    "Error interno al procesar la solicitud"));
            }
        }

        /// <summary>
        /// Busca productos por término (Server Streaming)
        /// </summary>
        public override async Task SearchProducts(
            SearchProductsRequest request,
            IServerStreamWriter<ProductResponse> responseStream,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"SearchProducts llamado con término: '{request.SearchTerm}'");

                // Validar solicitud
                var validationResult = await _searchValidator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Validación fallida: {errors}");
                    throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
                }

                // Construir query de búsqueda
                var searchTerm = request.SearchTerm.ToLower();
                var query = _context.Products
                    .Where(p => p.IsActive &&
                        (p.Name.ToLower().Contains(searchTerm) ||
                         p.Description.ToLower().Contains(searchTerm) ||
                         p.Category.ToLower().Contains(searchTerm)));

                // Ordenar por relevancia (nombre primero)
                query = query.OrderBy(p => p.Name.ToLower().Contains(searchTerm) ? 0 : 1)
                    .ThenBy(p => p.Name);

                // Aplicar paginación
                var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
                var pageSize = request.PageSize > 0 ? request.PageSize : 10;
                var skip = (pageNumber - 1) * pageSize;

                var products = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation($"Búsqueda encontró {products.Count} productos");

                // Enviar resultados en streaming
                foreach (var product in products)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("SearchProducts cancelado por el cliente");
                        break;
                    }

                    var response = MapToProductResponse(product);
                    await responseStream.WriteAsync(response);
                }

                _logger.LogInformation($"SearchProducts completado - {products.Count} resultados enviados");
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar productos");
                throw new RpcException(new Status(StatusCode.Internal,
                    "Error interno al procesar la solicitud"));
            }
        }

        /// <summary>
        /// Mapea una entidad Product a un mensaje ProductResponse
        /// </summary>
        private ProductResponse MapToProductResponse(Product product)
        {
            return new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = Convert.ToDouble(product.Price),
                Stock = product.Stock,
                Category = product.Category,
                CreatedAt = product.CreatedAt.ToString("o"),
                UpdatedAt = product.UpdatedAt?.ToString("o") ?? string.Empty,
                IsActive = product.IsActive
            };
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // MÉTODOS DE RESERVA DE STOCK - AGREGAR A ProductGrpcService.cs
        // ═══════════════════════════════════════════════════════════════════════════════
        // 
        // INSTRUCCIONES:
        // 1. Copiar estos métodos al final de la clase ProductGrpcService (antes del último })
        // 2. Agregar using para el mapper al inicio del archivo:
        //    using ProductService.gRPC.Mappers;
        // 3. Compilar: dotnet build
        //
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reserva stock temporalmente para una orden (paso 1 de saga)
        /// </summary>
        public override async Task<ReserveStockResponse> ReserveStock(
            ReserveStockRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation("📦 Reservando stock - ProductId: {ProductId}, Quantity: {Quantity}, OrderId: {OrderId}",
                request.ProductId, request.Quantity, request.OrderId);

            try
            {
                // Buscar producto
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("Producto {ProductId} no encontrado", request.ProductId);
                    return StockReservationMapper.ToReserveStockErrorResponse(
                        request.ProductId,
                        request.Quantity,
                        $"Producto {request.ProductId} no encontrado");
                }

                // Calcular stock disponible (stock real - reservas activas)
                var activeReservations = await _context.StockReservations
                    .Where(r => r.ProductId == request.ProductId && r.Status == ReservationStatus.Reserved)
                    .SumAsync(r => r.QuantityReserved);

                var availableStock = product.Stock - activeReservations;

                _logger.LogInformation("Stock disponible para Product {ProductId}: Total={Total}, Reservado={Reserved}, Disponible={Available}",
                    request.ProductId, product.Stock, activeReservations, availableStock);

                // Verificar disponibilidad
                if (availableStock < request.Quantity)
                {
                    _logger.LogWarning("Stock insuficiente - Disponible: {Available}, Solicitado: {Requested}",
                        availableStock, request.Quantity);
                    return StockReservationMapper.ToReserveStockErrorResponse(
                        request.ProductId,
                        request.Quantity,
                        $"Stock insuficiente. Disponible: {availableStock}, Solicitado: {request.Quantity}");
                }

                // Crear reserva
                var reservation = new StockReservation
                {
                    ReservationId = Guid.NewGuid().ToString(),
                    ProductId = request.ProductId,
                    OrderId = request.OrderId,
                    QuantityReserved = request.Quantity,
                    Status = ReservationStatus.Reserved,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.StockReservations.AddAsync(reservation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✓ Stock reservado exitosamente - ReservationId: {ReservationId}", reservation.ReservationId);

                return StockReservationMapper.ToReserveStockResponse(
                    reservation,
                    true,
                    $"Stock reservado exitosamente. Disponible después de reserva: {availableStock - request.Quantity}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reservar stock");
                return StockReservationMapper.ToReserveStockErrorResponse(
                    request.ProductId,
                    request.Quantity,
                    $"Error interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Confirma una reserva y descuenta el stock real (commit de saga exitosa)
        /// </summary>
        public override async Task<ConfirmReservationResponse> ConfirmReservation(
            ConfirmReservationRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation("✅ Confirmando reserva - ReservationId: {ReservationId}", request.ReservationId);

            try
            {
                // Buscar reserva
                var reservation = await _context.StockReservations
                    .Include(r => r.Product)
                    .FirstOrDefaultAsync(r => r.ReservationId == request.ReservationId);

                if (reservation == null)
                {
                    _logger.LogWarning("Reserva {ReservationId} no encontrada", request.ReservationId);
                    return StockReservationMapper.ToConfirmReservationErrorResponse(
                        request.ReservationId,
                        "Reserva no encontrada");
                }

                // Verificar que esté en estado Reserved
                if (reservation.Status != ReservationStatus.Reserved)
                {
                    _logger.LogWarning("Reserva {ReservationId} no está en estado Reserved (estado actual: {Status})",
                        request.ReservationId, reservation.Status);
                    return StockReservationMapper.ToConfirmReservationErrorResponse(
                        request.ReservationId,
                        $"La reserva no puede ser confirmada. Estado actual: {reservation.Status}");
                }

                // Descontar stock real del producto
                if (reservation.Product == null)
                {
                    reservation.Product = await _context.Products.FindAsync(reservation.ProductId);
                }

                if (reservation.Product!.Stock < reservation.QuantityReserved)
                {
                    _logger.LogError("Stock real insuficiente al confirmar - Stock: {Stock}, Reservado: {Reserved}",
                        reservation.Product.Stock, reservation.QuantityReserved);
                    return StockReservationMapper.ToConfirmReservationErrorResponse(
                        request.ReservationId,
                        "Stock real insuficiente para confirmar reserva");
                }

                // DESCONTAR STOCK REAL
                reservation.Product.Stock -= reservation.QuantityReserved;
                _logger.LogInformation("Stock descontado - Product {ProductId}: Nuevo stock = {NewStock}",
                    reservation.ProductId, reservation.Product.Stock);

                // Marcar reserva como confirmada
                reservation.Status = ReservationStatus.Confirmed;
                reservation.ConfirmedAt = DateTime.UtcNow;

                _context.StockReservations.Update(reservation);
                _context.Products.Update(reservation.Product);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✓ Reserva confirmada exitosamente - Stock descontado permanentemente");

                return StockReservationMapper.ToConfirmReservationResponse(
                    reservation,
                    true,
                    $"Reserva confirmada. Stock actual del producto: {reservation.Product.Stock}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al confirmar reserva");
                return StockReservationMapper.ToConfirmReservationErrorResponse(
                    request.ReservationId,
                    $"Error interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Libera una reserva (rollback de saga fallida)
        /// </summary>
        public override async Task<ReleaseReservationResponse> ReleaseReservation(
            ReleaseReservationRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation("🔄 Liberando reserva - ReservationId: {ReservationId}, Reason: {Reason}",
                request.ReservationId, request.Reason);

            try
            {
                // Buscar reserva
                var reservation = await _context.StockReservations
                    .FirstOrDefaultAsync(r => r.ReservationId == request.ReservationId);

                if (reservation == null)
                {
                    _logger.LogWarning("Reserva {ReservationId} no encontrada", request.ReservationId);
                    return StockReservationMapper.ToReleaseReservationErrorResponse(
                        request.ReservationId,
                        "Reserva no encontrada");
                }

                // Verificar que NO esté ya confirmada (no se puede liberar si ya se descontó el stock)
                if (reservation.Status == ReservationStatus.Confirmed)
                {
                    _logger.LogWarning("No se puede liberar reserva {ReservationId} - Ya fue confirmada",
                        request.ReservationId);
                    return StockReservationMapper.ToReleaseReservationErrorResponse(
                        request.ReservationId,
                        "La reserva ya fue confirmada y no puede ser liberada");
                }

                // Si ya fue liberada, retornar éxito (idempotencia)
                if (reservation.Status == ReservationStatus.Released)
                {
                    _logger.LogInformation("Reserva {ReservationId} ya fue liberada previamente", request.ReservationId);
                    return StockReservationMapper.ToReleaseReservationResponse(
                        reservation,
                        true,
                        "Reserva ya había sido liberada anteriormente");
                }

                // Marcar reserva como liberada
                reservation.Status = ReservationStatus.Released;
                reservation.ReleasedAt = DateTime.UtcNow;
                reservation.ReleaseReason = request.Reason;

                _context.StockReservations.Update(reservation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✓ Reserva liberada exitosamente - Stock nuevamente disponible");

                return StockReservationMapper.ToReleaseReservationResponse(
                    reservation,
                    true,
                    "Reserva liberada. El stock está nuevamente disponible.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al liberar reserva");
                return StockReservationMapper.ToReleaseReservationErrorResponse(
                    request.ReservationId,
                    $"Error interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica disponibilidad de stock sin modificar nada
        /// </summary>
        public override async Task<CheckAvailabilityResponse> CheckAvailability(
            CheckAvailabilityRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation("🔍 Verificando disponibilidad - ProductId: {ProductId}, Quantity: {Quantity}",
                request.ProductId, request.Quantity);

            try
            {
                // Buscar producto
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("Producto {ProductId} no encontrado", request.ProductId);
                    return StockReservationMapper.ToCheckAvailabilityErrorResponse(
                        request.ProductId,
                        $"Producto {request.ProductId} no encontrado");
                }

                // Calcular stock reservado activo
                var activeReservations = await _context.StockReservations
                    .Where(r => r.ProductId == request.ProductId && r.Status == ReservationStatus.Reserved)
                    .SumAsync(r => r.QuantityReserved);

                _logger.LogInformation("Disponibilidad - Product {ProductId}: Total={Total}, Reservado={Reserved}, Disponible={Available}",
                    request.ProductId, product.Stock, activeReservations, product.Stock - activeReservations);

                return StockReservationMapper.ToCheckAvailabilityResponse(
                    product,
                    activeReservations,
                    request.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar disponibilidad");
                return StockReservationMapper.ToCheckAvailabilityErrorResponse(
                    request.ProductId,
                    $"Error interno: {ex.Message}");
            }
        }

        #region reservation sin mapper
        ////Reserva de stock para una orden (saga)
        ///// <summary>
        ///// Reserva stock temporalmente para una orden (parte de saga)
        ///// </summary>
        //public override async Task<ReserveStockResponse> ReserveStock(
        //    ReserveStockRequest request,
        //    ServerCallContext context)
        //{
        //    _logger.LogInformation("Reservando stock - Product {ProductId}, Quantity: {Quantity}, Order: {OrderId}",
        //        request.ProductId, request.Quantity, request.OrderId);

        //    try
        //    {
        //        var product = await _context.Products
        //            .FirstOrDefaultAsync(p => p.Id == request.ProductId);

        //        if (product == null)
        //        {
        //            return new ReserveStockResponse
        //            {
        //                Success = false,
        //                Message = $"Product {request.ProductId} not found"
        //            };
        //        }

        //        // Calcular stock disponible (stock actual - reservas activas)
        //        var activeReservations = await _context.StockReservations
        //            .Where(r => r.ProductId == request.ProductId && r.Status == ReservationStatus.Reserved)
        //            .SumAsync(r => r.QuantityReserved);

        //        var availableStock = product.Stock - activeReservations;

        //        if (availableStock < request.Quantity)
        //        {
        //            _logger.LogWarning("Stock insuficiente - Product {ProductId}, Disponible: {Available}, Solicitado: {Requested}",
        //                request.ProductId, availableStock, request.Quantity);

        //            return new ReserveStockResponse
        //            {
        //                Success = false,
        //                ProductId = request.ProductId,
        //                RemainingStock = availableStock,
        //                Message = $"Insufficient stock. Available: {availableStock}, Requested: {request.Quantity}"
        //            };
        //        }

        //        // Crear reserva
        //        var reservation = new StockReservation
        //        {
        //            ReservationId = Guid.NewGuid().ToString(),
        //            ProductId = request.ProductId,
        //            OrderId = request.OrderId,
        //            QuantityReserved = request.Quantity,
        //            Status = ReservationStatus.Reserved,
        //            CreatedAt = DateTime.UtcNow
        //        };

        //        await _context.StockReservations.AddAsync(reservation);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation("✓ Stock reservado exitosamente - ReservationId: {ReservationId}, Product: {ProductId}, Quantity: {Quantity}",
        //            reservation.ReservationId, request.ProductId, request.Quantity);

        //        return new ReserveStockResponse
        //        {
        //            Success = true,
        //            ReservationId = reservation.ReservationId,
        //            ProductId = request.ProductId,
        //            QuantityReserved = request.Quantity,
        //            RemainingStock = availableStock - request.Quantity,
        //            Message = "Stock reserved successfully"
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error al reservar stock para Product {ProductId}", request.ProductId);
        //        return new ReserveStockResponse
        //        {
        //            Success = false,
        //            Message = $"Internal error: {ex.Message}"
        //        };
        //    }
        //}

        ///// <summary>
        ///// Confirma una reserva (commit de saga exitosa)
        ///// </summary>
        //public override async Task<ReservationResponse> ConfirmReservation(
        //    ConfirmReservationRequest request,
        //    ServerCallContext context)
        //{
        //    _logger.LogInformation("Confirmando reserva {ReservationId}", request.ReservationId);

        //    try
        //    {
        //        var reservation = await _context.StockReservations
        //            .Include(r => r.Product)
        //            .FirstOrDefaultAsync(r => r.ReservationId == request.ReservationId);

        //        if (reservation == null)
        //        {
        //            return new ReservationResponse
        //            {
        //                Success = false,
        //                Message = $"Reservation {request.ReservationId} not found"
        //            };
        //        }

        //        if (reservation.Status != ReservationStatus.Reserved)
        //        {
        //            return new ReservationResponse
        //            {
        //                Success = false,
        //                Message = $"Reservation already {reservation.Status}"
        //            };
        //        }

        //        // Descontar del stock real
        //        if (reservation.Product != null)
        //        {
        //            reservation.Product.Stock -= reservation.QuantityReserved;
        //            _context.Products.Update(reservation.Product);
        //        }

        //        // Marcar reserva como confirmada
        //        reservation.Status = ReservationStatus.Confirmed;
        //        reservation.ConfirmedAt = DateTime.UtcNow;
        //        _context.StockReservations.Update(reservation);

        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation("✓ Reserva confirmada - ReservationId: {ReservationId}, Product: {ProductId}, Quantity: {Quantity}",
        //            request.ReservationId, reservation.ProductId, reservation.QuantityReserved);

        //        return new ReservationResponse
        //        {
        //            Success = true,
        //            Message = "Reservation confirmed and stock deducted"
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error al confirmar reserva {ReservationId}", request.ReservationId);
        //        return new ReservationResponse
        //        {
        //            Success = false,
        //            Message = $"Internal error: {ex.Message}"
        //        };
        //    }
        //}

        ///// <summary>
        ///// Libera una reserva (rollback de saga fallida)
        ///// </summary>
        //public override async Task<ReservationResponse> ReleaseReservation(
        //    ReleaseReservationRequest request,
        //    ServerCallContext context)
        //{
        //    _logger.LogInformation("Liberando reserva {ReservationId}, Razón: {Reason}",
        //        request.ReservationId, request.Reason);

        //    try
        //    {
        //        var reservation = await _context.StockReservations
        //            .FirstOrDefaultAsync(r => r.ReservationId == request.ReservationId);

        //        if (reservation == null)
        //        {
        //            return new ReservationResponse
        //            {
        //                Success = false,
        //                Message = $"Reservation {request.ReservationId} not found"
        //            };
        //        }

        //        if (reservation.Status != ReservationStatus.Reserved)
        //        {
        //            return new ReservationResponse
        //            {
        //                Success = false,
        //                Message = $"Reservation already {reservation.Status}"
        //            };
        //        }

        //        // Marcar reserva como liberada
        //        reservation.Status = ReservationStatus.Released;
        //        reservation.ReleasedAt = DateTime.UtcNow;
        //        reservation.ReleaseReason = request.Reason;
        //        _context.StockReservations.Update(reservation);

        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation("✓ Reserva liberada - ReservationId: {ReservationId}, Razón: {Reason}",
        //            request.ReservationId, request.Reason);

        //        return new ReservationResponse
        //        {
        //            Success = true,
        //            Message = "Reservation released successfully"
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error al liberar reserva {ReservationId}", request.ReservationId);
        //        return new ReservationResponse
        //        {
        //            Success = false,
        //            Message = $"Internal error: {ex.Message}"
        //        };
        //    }
        //}

        ///// <summary>
        ///// Verifica disponibilidad de stock
        ///// </summary>
        //public override async Task<AvailabilityResponse> CheckAvailability(
        //    CheckAvailabilityRequest request,
        //    ServerCallContext context)
        //{
        //    try
        //    {
        //        var product = await _context.Products
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(p => p.Id == request.ProductId);

        //        if (product == null)
        //        {
        //            return new AvailabilityResponse
        //            {
        //                Available = false,
        //                CurrentStock = 0,
        //                ReservedStock = 0,
        //                AvailableStock = 0
        //            };
        //        }

        //        var reservedStock = await _context.StockReservations
        //            .Where(r => r.ProductId == request.ProductId && r.Status == ReservationStatus.Reserved)
        //            .SumAsync(r => r.QuantityReserved);

        //        var availableStock = product.Stock - reservedStock;

        //        return new AvailabilityResponse
        //        {
        //            Available = availableStock >= request.Quantity,
        //            CurrentStock = product.Stock,
        //            ReservedStock = reservedStock,
        //            AvailableStock = availableStock
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error al verificar disponibilidad Product {ProductId}", request.ProductId);
        //        throw new RpcException(new Status(StatusCode.Internal, $"Internal error: {ex.Message}"));
        //    }
        //}
        #endregion reservation sin mappers
    }
}
