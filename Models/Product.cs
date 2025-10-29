using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ProductService.gRPC.Models
{

    /// <summary>
    /// Entidad que representa un producto en el catálogo de e-commerce
    /// </summary>
    [Table("Products")]
    public class Product
    {
        /// <summary>
        /// Identificador único del producto
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Nombre del producto
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descripción detallada del producto
        /// </summary>
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Precio unitario del producto
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        /// <summary>
        /// Cantidad disponible en inventario
        /// </summary>
        [Required]
        public int Stock { get; set; }

        /// <summary>
        /// Categoría de clasificación del producto
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora de creación del registro
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha y hora de la última modificación
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Indicador de estado activo del producto
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;
    }

}