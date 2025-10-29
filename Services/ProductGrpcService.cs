using FluentValidation;
using global::ECommerceGRPC.ProductService;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProductService.gRPC.Data;
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
    }
}
