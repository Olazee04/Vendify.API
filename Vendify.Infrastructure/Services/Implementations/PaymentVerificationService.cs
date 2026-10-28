using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Enums;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class PaymentVerificationService : IPaymentVerificationService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentVerificationService> _logger;

        public PaymentVerificationService(
            HttpClient http,
            IConfiguration config,
            ILogger<PaymentVerificationService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public async Task<PaymentVerificationResult> VerifyAsync(
            PaymentMethod method,
            string reference,
            decimal expectedAmountNaira)
        {
            try
            {
                switch (method)
                {
                    case PaymentMethod.Paystack:
                        return await VerifyPaystackAsync(
                            reference, expectedAmountNaira);
                    case PaymentMethod.Flutterwave:
                        return await VerifyFlutterwaveAsync(
                            reference, expectedAmountNaira);
                    default:
                        return new PaymentVerificationResult(false,
                            "This payment method cannot be verified online.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Payment verification failed for reference {Reference}",
                    reference);
                return new PaymentVerificationResult(false,
                    "We could not confirm your payment right now. " +
                    "Please contact the store with your payment reference.");
            }
        }

        // ── Paystack ───────────────────────────────────────
        private async Task<PaymentVerificationResult> VerifyPaystackAsync(
            string reference, decimal expectedNaira)
        {
            var secret = _config["Paystack:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("Paystack:SecretKey is not configured.");
                return new PaymentVerificationResult(false,
                    "Online payment is not set up correctly. " +
                    "Please contact the store.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.paystack.co/transaction/verify/" +
                Uri.EscapeDataString(reference));
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", secret);

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack verify returned {Status}: {Body}",
                    (int)response.StatusCode, body);
                return new PaymentVerificationResult(false,
                    "We could not find that payment.");
            }

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");

            var status = data.GetProperty("status").GetString();
            var currency = data.GetProperty("currency").GetString();
            var amountKobo = data.GetProperty("amount").GetDecimal();
            var expectedKobo = Math.Round(expectedNaira * 100m, 0);

            if (status != "success")
                return new PaymentVerificationResult(false,
                    "That payment was not successful.");

            if (!string.Equals(currency, "NGN",
                    StringComparison.OrdinalIgnoreCase))
                return new PaymentVerificationResult(false,
                    "Payment must be made in Naira.");

            if (amountKobo < expectedKobo)
                return new PaymentVerificationResult(false,
                    "The amount paid is less than the order total.");

            return new PaymentVerificationResult(true, "Payment verified");
        }

        // ── Flutterwave ────────────────────────────────────
        private async Task<PaymentVerificationResult> VerifyFlutterwaveAsync(
            string reference, decimal expectedNaira)
        {
            var secret = _config["Flutterwave:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("Flutterwave:SecretKey is not configured.");
                return new PaymentVerificationResult(false,
                    "Flutterwave payment is not set up correctly. " +
                    "Please contact the store.");
            }

            // Only digits means Flutterwave's own transaction number.
            // Anything else is our reference (the tx_ref we gave them).
            var isTransactionId = reference.All(char.IsDigit);

            var url = isTransactionId
                ? "https://api.flutterwave.com/v3/transactions/" +
                    Uri.EscapeDataString(reference) + "/verify"
                : "https://api.flutterwave.com/v3/transactions/verify_by_reference?tx_ref=" +
                    Uri.EscapeDataString(reference);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", secret);

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Flutterwave verify returned {Status}: {Body}",
                    (int)response.StatusCode, body);
                return new PaymentVerificationResult(false,
                    "We could not find that payment.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var top) &&
                top.GetString() != "success")
                return new PaymentVerificationResult(false,
                    "We could not confirm that payment.");

            var data = root.GetProperty("data");

            // The payment must be the one made for THIS reference
            if (!isTransactionId)
            {
                var txRef = data.TryGetProperty("tx_ref", out var t)
                    ? t.GetString() : null;
                if (!string.Equals(txRef, reference, StringComparison.Ordinal))
                    return new PaymentVerificationResult(false,
                        "That payment does not match this order.");
            }

            var status = data.GetProperty("status").GetString();
            var currency = data.GetProperty("currency").GetString();
            var amount = data.GetProperty("amount").GetDecimal();

            if (!string.Equals(status, "successful",
                    StringComparison.OrdinalIgnoreCase))
                return new PaymentVerificationResult(false,
                    "That payment was not successful.");

            if (!string.Equals(currency, "NGN",
                    StringComparison.OrdinalIgnoreCase))
                return new PaymentVerificationResult(false,
                    "Payment must be made in Naira.");

            if (amount < expectedNaira)
                return new PaymentVerificationResult(false,
                    "The amount paid is less than the order total.");

            return new PaymentVerificationResult(true, "Payment verified");
        }
    }
}