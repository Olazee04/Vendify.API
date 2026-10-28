using System.Text.Json;

namespace Vendify.Application.DTOs.Payment
{
    public class CreatePaymentIntentRequest
    {
        // "Order", "StockpileDeposit" or "StockpileCheckout"
        public string Kind { get; set; } = string.Empty;

        // The same details the website would normally send
        public JsonElement Payload { get; set; }
    }

    public class PaymentIntentDto
    {
        public string Reference { get; set; } = string.Empty;
    }

    public class PaymentFulfilDto
    {
        public string Kind { get; set; } = string.Empty;
        public string Status { get; set; } = "Fulfilled";
        public string? Message { get; set; }
        public string? OrderNumber { get; set; }
        public decimal? ItemsTotal { get; set; }
        public decimal? ShippingFee { get; set; }
        public decimal? OrderTotal { get; set; }
    }

    // Internal result used by the controller (not sent to the website as is)
    public class FulfilResult
    {
        public bool Success { get; set; }
        public bool NotFound { get; set; }
        // true = it may still work if tried again later
        public bool Retry { get; set; }
        // true = give up, retrying will not help
        public bool Permanent { get; set; }
        public string Message { get; set; } = string.Empty;
        public PaymentFulfilDto? Data { get; set; }

        public static FulfilResult Ok(PaymentFulfilDto data, string message) =>
            new() { Success = true, Data = data, Message = message };

        public static FulfilResult Fail(
            string message, bool permanent = false) =>
            new() { Success = false, Message = message, Permanent = permanent };

        public static FulfilResult Missing() =>
            new()
            {
                Success = false,
                NotFound = true,
                Message = "Payment reference not found."
            };
    }
}