using Vendify.Core.Enums;

namespace Vendify.Core.Entities
{
    public class Order : BaseEntity
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
        public PaymentMethod PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public decimal Total { get; set; }
        public string? CouponCode { get; set; }
        public string? Notes { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        // Owned Entity
        public ShippingAddress ShippingAddress { get; set; } = new();

        // Foreign Keys
        public Guid StoreId { get; set; }
        public Guid? CustomerId { get; set; }

        // Navigation
        public Store Store { get; set; } = null!;
        public User? Customer { get; set; }
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}