using Microsoft.EntityFrameworkCore;
using ProductService.gRPC.Models;

namespace ProductService.gRPC.Data
{
    /// <summary>
    /// Contexto de base de datos para el servicio de productos
    /// </summary>
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Colección de productos en la base de datos
        /// </summary>
        public DbSet<Product> Products { get; set; } = null!;

        /// <summary>
        /// Configuración del modelo de datos
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de la entidad Product
            modelBuilder.Entity<Product>(entity =>
            {
                // Configuración de índices para optimizar consultas
                entity.HasIndex(p => p.Name)
                    .HasDatabaseName("IX_Products_Name");

                entity.HasIndex(p => p.Category)
                    .HasDatabaseName("IX_Products_Category");

                entity.HasIndex(p => p.IsActive)
                    .HasDatabaseName("IX_Products_IsActive");

                entity.HasIndex(p => p.Price)
                    .HasDatabaseName("IX_Products_Price");

                // Configuración de restricciones
                entity.Property(p => p.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.Description)
                    .HasMaxLength(1000);

                entity.Property(p => p.Category)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.Price)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(p => p.Stock)
                    .IsRequired();

                entity.Property(p => p.CreatedAt)
                    .IsRequired()
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(p => p.IsActive)
                    .IsRequired()
                    .HasDefaultValue(true);

                // Validación a nivel de base de datos
                //entity.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
                //entity.HasCheckConstraint("CK_Products_Stock", "[Stock] >= 0");
            });
        }
    }
}