namespace Vendify.Application.DTOs.Payments
{
    public class PaymentAttentionItemDto
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        // Order, StockpileDeposit or StockpileCheckout
        public string Kind { get; set; } = string.Empty;
        public string Provider { get; set; } = "Paystack";
        // Failed, Retrying, Stuck or Resolved
        public string Status { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public int ItemCount { get; set; }
    }

    public class PaymentAttentionListDto
    {
        public List<PaymentAttentionItemDto> Items { get; set; } = new();
        public int OpenCount { get; set; }
    }

    public class ResolvePaymentRequest
    {
        public string? Note { get; set; }
    }
}