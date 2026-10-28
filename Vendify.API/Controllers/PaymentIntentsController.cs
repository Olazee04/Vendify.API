using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Payment;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/payments")]
    public class PaymentIntentsController : ControllerBase
    {
        private readonly IPaymentFulfilmentService _fulfilment;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentIntentsController> _logger;

        public PaymentIntentsController(
            IPaymentFulfilmentService fulfilment,
            IConfiguration config,
            ILogger<PaymentIntentsController> logger)
        {
            _fulfilment = fulfilment;
            _config = config;
            _logger = logger;
        }

        // POST /api/v1/payments/intent/{storeSlug}
        // Called by the website BEFORE the customer pays
        [HttpPost("intent/{storeSlug}")]
        public async Task<IActionResult> CreateIntent(
            string storeSlug, [FromBody] CreatePaymentIntentRequest request)
        {
            var result = await _fulfilment.CreateIntentAsync(storeSlug, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/payments/fulfil/{reference}
        // Called by the website right AFTER the customer pays
        [HttpPost("fulfil/{reference}")]
        public async Task<IActionResult> Fulfil(string reference)
        {
            var r = await _fulfilment.FulfilAsync(reference);

            if (r.Success)
                return Ok(ApiResponse<PaymentFulfilDto>
                    .SuccessResponse(r.Data!, r.Message));

            if (r.NotFound)
                return NotFound(ApiResponse<PaymentFulfilDto>
                    .FailureResponse(r.Message));

            return BadRequest(ApiResponse<PaymentFulfilDto>
                .FailureResponse(r.Message));
        }

        // POST /api/v1/payments/paystack/webhook
        // Called by Paystack itself
        [HttpPost("paystack/webhook")]
        public async Task<IActionResult> PaystackWebhook()
        {
            var body = await ReadBodyAsync();

            var secret = _config["Paystack:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("Paystack:SecretKey is not configured.");
                return StatusCode(500);
            }

            var sent = Request.Headers["x-paystack-signature"]
                .ToString().Trim().ToLowerInvariant();

            var expected = Convert.ToHexString(
                HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), body))
                .ToLowerInvariant();

            if (sent.Length == 0 || !SameText(sent, expected))
            {
                _logger.LogWarning("Paystack webhook with a bad signature.");
                return Unauthorized();
            }

            string? reference;
            string? status;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var eventName = root.GetProperty("event").GetString();

                if (eventName != "charge.success")
                    return Ok();   // we only care about successful charges

                var data = root.GetProperty("data");
                reference = data.GetProperty("reference").GetString();
                status = data.GetProperty("status").GetString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read a Paystack webhook.");
                return Ok();
            }

            if (status != "success" || string.IsNullOrWhiteSpace(reference))
                return Ok();

            return await RecordAsync(reference, "Paystack");
        }

        // POST /api/v1/payments/flutterwave/webhook
        // Called by Flutterwave itself
        [HttpPost("flutterwave/webhook")]
        public async Task<IActionResult> FlutterwaveWebhook()
        {
            var body = await ReadBodyAsync();

            var secretHash = _config["Flutterwave:SecretHash"];
            if (string.IsNullOrWhiteSpace(secretHash))
            {
                _logger.LogError("Flutterwave:SecretHash is not configured.");
                return StatusCode(500);
            }

            // Flutterwave sends your secret hash in "verif-hash"
            // (newer accounts may sign the body in "flutterwave-signature")
            var sentHash = Request.Headers["verif-hash"].ToString().Trim();
            var sentSignature = Request.Headers["flutterwave-signature"]
                .ToString().Trim();

            var valid = sentHash.Length > 0 && SameText(sentHash, secretHash);

            if (!valid && sentSignature.Length > 0)
            {
                var expectedSignature = Convert.ToBase64String(
                    HMACSHA256.HashData(
                        Encoding.UTF8.GetBytes(secretHash), body));
                valid = SameText(sentSignature, expectedSignature);
            }

            if (!valid)
            {
                _logger.LogWarning("Flutterwave webhook with a bad signature.");
                return Unauthorized();
            }

            string? reference;
            string? status;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var eventName = root.TryGetProperty("event", out var ev)
                    ? ev.GetString() : null;

                if (eventName != "charge.completed")
                    return Ok();   // we only care about finished charges

                var data = root.GetProperty("data");
                reference = data.GetProperty("tx_ref").GetString();
                status = data.GetProperty("status").GetString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read a Flutterwave webhook.");
                return Ok();
            }

            if (!string.Equals(status, "successful",
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(reference))
                return Ok();

            return await RecordAsync(reference, "Flutterwave");
        }

        // ── Shared helpers ──────────────────────────────────

        // Records the order / deposit / checkout for a paid reference
        private async Task<IActionResult> RecordAsync(
            string reference, string provider)
        {
            var r = await _fulfilment.FulfilAsync(reference);

            if (r.Success)
            {
                _logger.LogInformation(
                    "{Provider} webhook recorded payment {Reference}",
                    provider, reference);
                return Ok();
            }

            if (r.NotFound)
            {
                // Not one of ours (for example an older payment)
                _logger.LogInformation(
                    "{Provider} webhook for unknown reference {Reference}",
                    provider, reference);
                return Ok();
            }

            // A 500 makes the provider try again later
            return r.Retry ? StatusCode(500) : Ok();
        }

        private async Task<byte[]> ReadBodyAsync()
        {
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static bool SameText(string a, string b) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }
}