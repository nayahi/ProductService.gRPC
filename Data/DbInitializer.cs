using Microsoft.EntityFrameworkCore;
using ProductService.gRPC.Models;

namespace ProductService.gRPC.Data
{

    /// <summary>
    /// Inicializador de base de datos con datos de prueba
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// Inicializa la base de datos y carga datos de prueba si está vacía
        /// </summary>
        public static async Task InitializeAsync(ProductDbContext context, ILogger logger)
        {
            try
            {
                // Asegurar que la base de datos esté creada
                logger.LogInformation("Verificando existencia de base de datos...");
                await context.Database.EnsureCreatedAsync();

                // Aplicar migraciones pendientes
                if (context.Database.GetPendingMigrations().Any())
                {
                    logger.LogInformation("Aplicando migraciones pendientes...");
                    await context.Database.MigrateAsync();
                }

                // Verificar si ya existen productos
                if (await context.Products.AnyAsync())
                {
                    logger.LogInformation("Base de datos ya contiene productos. Omitiendo inicialización.");
                    return;
                }

                logger.LogInformation("Inicializando base de datos con datos de prueba...");

                // Crear productos de prueba
                var products = new List<Product>
                {
                    new Product
                    {
                        Name = "Laptop Dell XPS 15",
                        Description = "Laptop de alto rendimiento con procesador Intel Core i7, 16GB RAM, SSD 512GB",
                        Price = 1299.99m,
                        Stock = 15,
                        Category = "Electrónica",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Mouse Logitech MX Master 3",
                        Description = "Mouse inalámbrico ergonómico con precisión de 4000 DPI",
                        Price = 99.99m,
                        Stock = 50,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Teclado Mecánico Corsair K95",
                        Description = "Teclado mecánico RGB con switches Cherry MX",
                        Price = 189.99m,
                        Stock = 30,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Monitor Samsung 27\" 4K",
                        Description = "Monitor 4K UHD de 27 pulgadas con tecnología HDR",
                        Price = 449.99m,
                        Stock = 20,
                        Category = "Electrónica",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Silla Ergonómica Herman Miller",
                        Description = "Silla de oficina ergonómica con soporte lumbar ajustable",
                        Price = 799.99m,
                        Stock = 10,
                        Category = "Muebles",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Webcam Logitech C920",
                        Description = "Webcam Full HD 1080p con micrófono estéreo",
                        Price = 79.99m,
                        Stock = 40,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Auriculares Sony WH-1000XM4",
                        Description = "Auriculares inalámbricos con cancelación de ruido activa",
                        Price = 349.99m,
                        Stock = 25,
                        Category = "Audio",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Escritorio Ajustable en Altura",
                        Description = "Escritorio de pie eléctrico con altura ajustable de 60cm a 120cm",
                        Price = 599.99m,
                        Stock = 8,
                        Category = "Muebles",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Hub USB-C 7 en 1",
                        Description = "Adaptador multipuerto USB-C con HDMI, USB 3.0 y lector SD",
                        Price = 49.99m,
                        Stock = 60,
                        Category = "Accesorios",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Lámpara de Escritorio LED",
                        Description = "Lámpara LED regulable con carga inalámbrica Qi integrada",
                        Price = 69.99m,
                        Stock = 35,
                        Category = "Iluminación",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "SSD Samsung 970 EVO 1TB",
                        Description = "Disco sólido NVMe M.2 de alta velocidad con 1TB de capacidad",
                        Price = 129.99m,
                        Stock = 45,
                        Category = "Almacenamiento",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    },
                    new Product
                    {
                        Name = "Router Wi-Fi 6 ASUS",
                        Description = "Router Wi-Fi 6 de doble banda con cobertura de 3000 pies cuadrados",
                        Price = 179.99m,
                        Stock = 18,
                        Category = "Redes",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    }
                };

                // Agregar productos a la base de datos
                await context.Products.AddRangeAsync(products);
                await context.SaveChangesAsync();

                logger.LogInformation($"Base de datos inicializada exitosamente con {products.Count} productos.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al inicializar la base de datos.");
                throw;
            }
        }
    }
}
