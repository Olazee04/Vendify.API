namespace Vendify.Application.DTOs.Stockpile
{
    public class MerchantStockpileSummaryDto
    {
        public int OpenCount { get; set; }
        public int ExpiredCount { get; set; }
        public int CheckedOutCount { get; set; }
        public int ReleasedCount { get; set; }
        // Money customers have paid for items still held (Open + Expired)
        public decimal AmountHeld { get; set; }
    }

    public class MerchantStockpileListItemDto
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        // Open, Expired, CheckedOut, Released (or CheckingOut / Releasing)
        public string Status { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalWeightKg { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? OrderNumber { get; set; }
    }

    public class MerchantStockpileListDto
    {
        public MerchantStockpileSummaryDto Summary { get; set; } = new();
        public List<MerchantStockpileListItemDto> Stockpiles { get; set; } = new();
    }

    public class MerchantDepositDto
    {
        public Guid Id { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RefundedAt { get; set; }
    }

    public class MerchantStockpileDetailDto : MerchantStockpileListItemDto
    {
        public List<StockpileViewItemDto> Items { get; set; } = new();
        public List<MerchantDepositDto> Deposits { get; set; } = new();
        public string? ReleaseNote { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
    }

    public class ExtendStockpileRequest
    {
        public int Days { get; set; }
    }

    public class ReleaseStockpileRequest
    {
        public string? Note { get; set; }
    }
}