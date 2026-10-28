using Vendify.Core.Enums;

namespace Vendify.Core.Entities
{
    // One open stockpile per customer (email) per store
    public class Stockpile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StoreId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public DateTime? ExpiresAt { get; set; }
        public Guid? OrderId { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string? ReleaseNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    // One verified payment that added items to a stockpile
    public class StockpileDeposit
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StockpileId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public DateTime? RefundedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // One product line inside a stockpile
    public class StockpileItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StockpileId { get; set; }
        public Guid DepositId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? WeightKg { get; set; }   // weight of ONE unit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // A one-time email code that lets a customer open their stockpile
    public class StockpileAccessCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StoreId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}