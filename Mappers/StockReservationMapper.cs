using global::ProductService.gRPC.Models;
using ProductService.gRPC.Models;

namespace ProductService.gRPC.Mappers
{

    /// <summary>
    /// Mapper para conversiones de StockReservation entre entidades y proto
    /// </summary>
    public static class StockReservationMapper
    {
        /// <summary>
        /// Mapea StockReservation a ReserveStockResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.ReserveStockResponse ToReserveStockResponse(
            StockReservation reservation,
            bool success,
            string message = "")
        {
            return new ECommerceGRPC.ProductService.ReserveStockResponse
            {
                Success = success,
                ReservationId = reservation.ReservationId,
                ProductId = reservation.ProductId,
                QuantityReserved = reservation.QuantityReserved,
                Message = message,
                ReservedAt = reservation.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }

        /// <summary>
        /// Mapea resultado de confirmación a ConfirmReservationResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.ConfirmReservationResponse ToConfirmReservationResponse(
            StockReservation reservation,
            bool success,
            string message = "")
        {
            return new ECommerceGRPC.ProductService.ConfirmReservationResponse
            {
                Success = success,
                ReservationId = reservation.ReservationId,
                ProductId = reservation.ProductId,
                QuantityConfirmed = reservation.QuantityReserved,
                Message = message,
                ConfirmedAt = reservation.ConfirmedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") ?? ""
            };
        }

        /// <summary>
        /// Mapea resultado de liberación a ReleaseReservationResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.ReleaseReservationResponse ToReleaseReservationResponse(
            StockReservation reservation,
            bool success,
            string message = "")
        {
            return new ECommerceGRPC.ProductService.ReleaseReservationResponse
            {
                Success = success,
                ReservationId = reservation.ReservationId,
                ProductId = reservation.ProductId,
                QuantityReleased = reservation.QuantityReserved,
                Message = message,
                ReleasedAt = reservation.ReleasedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") ?? "",
                Reason = reservation.ReleaseReason ?? ""
            };
        }

        /// <summary>
        /// Mapea información de disponibilidad a CheckAvailabilityResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.CheckAvailabilityResponse ToCheckAvailabilityResponse(
            Product product,
            int activeReservations,
            int requestedQuantity)
        {
            var availableStock = product.Stock - activeReservations;
            var available = availableStock >= requestedQuantity;

            return new ECommerceGRPC.ProductService.CheckAvailabilityResponse
            {
                ProductId = product.Id,
                ProductName = product.Name,
                TotalStock = product.Stock,
                ReservedStock = activeReservations,
                AvailableStock = availableStock,
                RequestedQuantity = requestedQuantity,
                Available = available,
                Message = available
                    ? $"Stock disponible: {availableStock} unidades"
                    : $"Stock insuficiente. Disponible: {availableStock}, Solicitado: {requestedQuantity}"
            };
        }

        /// <summary>
        /// Crea respuesta de error para ReserveStockResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.ReserveStockResponse ToReserveStockErrorResponse(
            int productId,
            int quantity,
            string errorMessage)
        {
            return new ECommerceGRPC.ProductService.ReserveStockResponse
            {
                Success = false,
                ReservationId = "",
                ProductId = productId,
                QuantityReserved = 0,
                Message = errorMessage,
                ReservedAt = ""
            };
        }

        /// <summary>
        /// Crea respuesta de error para ConfirmReservationResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.ConfirmReservationResponse ToConfirmReservationErrorResponse(
            string reservationId,
            string errorMessage)
        {
            return new ECommerceGRPC.ProductService.ConfirmReservationResponse
            {
                Success = false,
                ReservationId = reservationId,
                ProductId = 0,
                QuantityConfirmed = 0,
                Message = errorMessage,
                ConfirmedAt = ""
            };
        }

        /// <summary>
        /// Crea respuesta de error para ReleaseReservationResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.ReleaseReservationResponse ToReleaseReservationErrorResponse(
            string reservationId,
            string errorMessage)
        {
            return new ECommerceGRPC.ProductService.ReleaseReservationResponse
            {
                Success = false,
                ReservationId = reservationId,
                ProductId = 0,
                QuantityReleased = 0,
                Message = errorMessage,
                ReleasedAt = "",
                Reason = ""
            };
        }

        /// <summary>
        /// Crea respuesta de error para CheckAvailabilityResponse
        /// </summary>
        public static ECommerceGRPC.ProductService.CheckAvailabilityResponse ToCheckAvailabilityErrorResponse(
            int productId,
            string errorMessage)
        {
            return new ECommerceGRPC.ProductService.CheckAvailabilityResponse
            {
                ProductId = productId,
                ProductName = "",
                TotalStock = 0,
                ReservedStock = 0,
                AvailableStock = 0,
                RequestedQuantity = 0,
                Available = false,
                Message = errorMessage
            };
        }
    }
}
