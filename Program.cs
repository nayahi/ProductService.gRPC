using ProductService.gRPC.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProductService.gRPC.Data;
using ProductService.gRPC.Validators;
using Serilog;
using ECommerceGRPC.ProductService;

// Configurar Serilog para logging estructurado
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/productservice-.log", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Iniciando ProductService gRPC...");

    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog como proveedor de logging
    builder.Host.UseSerilog();

    // Configurar Entity Framework Core con SQL Server
    builder.Services.AddDbContext<ProductDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    });

    // Registrar validadores de FluentValidation
    builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
    builder.Services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
    builder.Services.AddScoped<IValidator<GetProductRequest>, GetProductRequestValidator>();
    builder.Services.AddScoped<IValidator<GetProductsRequest>, GetProductsRequestValidator>();
    builder.Services.AddScoped<IValidator<SearchProductsRequest>, SearchProductsRequestValidator>();
    builder.Services.AddScoped<IValidator<DeleteProductRequest>, DeleteProductRequestValidator>();

    // Registrar servicios gRPC
    builder.Services.AddGrpc(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
        options.MaxSendMessageSize = 4 * 1024 * 1024;    // 4 MB
    });

    // Configurar reflexión de gRPC para herramientas de desarrollo
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddGrpcReflection();
    }

    var app = builder.Build();

    // Inicializar base de datos y datos de prueba
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ProductDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();
            await DbInitializer.InitializeAsync(context, logger);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error al inicializar la base de datos");
            throw;
        }
    }

    // Configurar pipeline HTTP
    if (app.Environment.IsDevelopment())
    {
        app.MapGrpcReflectionService();
    }

    // Mapear servicio gRPC
    app.MapGrpcService<ProductGrpcService>();

    // Endpoint de salud básico
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        service = "ProductService.gRPC",
        timestamp = DateTime.UtcNow
    }));

    // Endpoint de información del servicio
    app.MapGet("/", () => Results.Ok(new
    {
        service = "ProductService gRPC",
        version = "1.0.0",
        description = "Servicio de gestión de productos con comunicación gRPC",
        endpoints = new[]
        {
            "GetProduct - Obtiene un producto por ID",
            "GetProducts - Lista productos con paginación (streaming)",
            "CreateProduct - Crea un nuevo producto",
            "UpdateProduct - Actualiza un producto existente",
            "DeleteProduct - Elimina un producto (lógico)",
            "SearchProducts - Busca productos por término (streaming)"
        },
        grpcPort = 7001,
        healthCheck = "/health"
    }));

    Log.Information("ProductService gRPC iniciado exitosamente en puerto 7001");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}