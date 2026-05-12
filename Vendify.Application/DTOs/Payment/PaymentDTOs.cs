using System.ComponentModel.DataAnnotations;

namespace Vendify.Application.DTOs.Payment
{
    // ─── REQUEST DTOs ────────────────────────────────────────

    public class InitiatePaymentRequest
    {
        [Required]
        public Guid OrderId { get; set; }

        [Required]
        public string Provider { get; set; } = string.Empty;
        // "paystack" | "flutterwave" | "stripe"

        public string? CallbackUrl { get; set; }
    }

    public class VerifyPaymentRequest
    {
        [Required]
        public string Reference { get; set; } = string.Empty;

        [Required]
        public string Provider { get; set; } = string.Empty;
    }

    public class StripePaymentRequest
    {
        [Required]
        public Guid OrderId { get; set; }
        public string Currency { get; set; } = "usd";
    }

    // ─── RESPONSE DTOs ───────────────────────────────────────

    public class PaymentInitiateDto
    {
        public string Provider { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
    }

    public class PaymentVerifyDto
    {
        public bool IsPaid { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
    }

    public class StripePaymentDto
    {
        public string ClientSecret { get; set; } = string.Empty;
        public string PaymentIntentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
    }

    // ─── WEBHOOK DTOs ────────────────────────────────────────

    public class PaystackWebhookDto
    {
        public string Event { get; set; } = string.Empty;
        public PaystackWebhookData Data { get; set; } = new();
    }

    public class PaystackWebhookData
    {
        public string Reference { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    public class FlutterwaveWebhookDto
    {
        public string Event { get; set; } = string.Empty;
        public FlutterwaveWebhookData Data { get; set; } = new();
    }

    public class FlutterwaveWebhookData
    {
        public string TxRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
    }
}