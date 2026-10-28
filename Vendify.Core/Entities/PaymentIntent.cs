namespace Vendify.Core.Entities
{
    // The details of a payment, saved BEFORE the customer pays.
    // Kind is "Order", "StockpileDeposit" or "StockpileCheckout".
    // Status is Pending, Processing, Fulfilled or Failed.
    public class PaymentIntent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Reference { get; set; } = string.Empty;
        public Guid StoreId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public int Attempts { get; set; }
        public string? FailureReason { get; set; }
        public string? ResultJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FulfilledAt { get; set; }
    }
}