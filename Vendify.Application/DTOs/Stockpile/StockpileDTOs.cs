using Vendify.Core.Enums;

namespace Vendify.Application.DTOs.Stockpile
{
    public static class StockpileMessages
    {
        public const string SessionExpired =
            "Your session has expired. Please sign in again.";
    }

    // ── Merchant settings ────────────────────────────────
    public class StockpileSettingsDto
    {
        public bool IsEnabled { get; set; }
        public int? MaxItems { get; set; }
        public decimal? MaxAmount { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public int? HoldDays { get; set; }
    }

    public class UpdateStockpileSettingsRequest
    {
        public bool IsEnabled { get; set; }
        public int? MaxItems { get; set; }
        public decimal? MaxAmount { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public int? HoldDays { get; set; }
    }

    // ── Customer: adding to a stockpile (Stage 2) ────────
    public class StockpilePublicInfoDto
    {
        public bool IsEnabled { get; set; }
        public int? MaxItems { get; set; }
        public decimal? MaxAmount { get; set; }
        public decimal? MaxWeightKg { get; set; }
        public int? HoldDays { get; set; }
    }

    public class StockpileItemRequest
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
    }

    public class StockpileCheckRequest
    {
        public string Token { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<StockpileItemRequest> Items { get; set; } = new();
    }

    public class StockpileDepositRequest
    {
        public string Token { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public List<StockpileItemRequest> Items { get; set; } = new();
    }

    public class StockpileCheckDto
    {
        public decimal Amount { get; set; }
        public int ItemCount { get; set; }
        public bool WillBeFull { get; set; }
    }

    public class StockpileDepositResultDto
    {
        public decimal AmountPaid { get; set; }
        public int ItemsAdded { get; set; }
        public bool IsFull { get; set; }
    }

    // ── Customer: viewing and checking out (Stage 3) ─────
    public class StockpileAccessRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class StockpileVerifyRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class StockpileTokenDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class StockpileViewRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public class StockpileViewItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? WeightKg { get; set; }
    }

    public class StockpileViewDto
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public List<StockpileViewItemDto> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalWeightKg { get; set; }
        public int? MaxItems { get; set; }
        public decimal? MaxAmount { get; set; }
        public decimal? MaxWeightKg { get; set; }
    }

    public class StockpileCheckoutAddress
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = "Nigeria";
        public string? PostalCode { get; set; }
    }

    public class StockpileCheckoutRequest
    {
        public Guid? ShippingZoneId { get; set; }
        public string Token { get; set; } = string.Empty;
        public StockpileCheckoutAddress? ShippingAddress { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
    }

    public class StockpileCheckoutResultDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public decimal ItemsTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal OrderTotal { get; set; }
    }
}