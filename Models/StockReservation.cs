using System.ComponentModel.DataAnnotations;

namespace ProductService.gRPC.Models
{
    /// <summary>
    /// Reserva temporal de stock para saga distribuida
    /// </summary>
    public class StockReservation
    {
        [Key]
        [MaxLength(50)]
        public string ReservationId { get; set; } = string.Empty;

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int QuantityReserved { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = ReservationStatus.Reserved;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedAt { get; set; }

        public DateTime? ReleasedAt { get; set; }

        [MaxLength(500)]
        public string? ReleaseReason { get; set; }

        // Relación con Product
        public Product? Product { get; set; }
    }

    public static class ReservationStatus
    {
        public const string Reserved = "Reserved";
        public const string Confirmed = "Confirmed";
        public const string Released = "Released";
    }
}
