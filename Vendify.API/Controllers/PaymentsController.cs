using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Payment;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IStoreService _storeService;

        public PaymentsController(
            IPaymentService paymentService,
            IStoreService storeService)
        {
            _paymentService = paymentService;
            _storeService = storeService;
        }

        // POST /api/v1/payments/initiate — Auth required
        [Authorize]
        [HttpPost("initiate")]
        public async Task<IActionResult> InitiatePayment(
            [FromBody] InitiatePaymentRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _paymentService
                .InitiatePaymentAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/payments/verify — Auth required
        [Authorize]
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment(
            [FromBody] VerifyPaymentRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _paymentService
                .VerifyPaymentAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/payments/stripe — Auth required
        [Authorize]
        [HttpPost("stripe")]
        public async Task<IActionResult> StripePayment(
            [FromBody] StripePaymentRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _paymentService
                .InitiateStripePaymentAsync(
                    request.OrderId,
                    storeId.Value,
                    request.Currency);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/v1/payments/callback — Public (redirect from payment page)
        [HttpGet("callback")]
        public async Task<IActionResult> PaymentCallback(
            [FromQuery] string reference,
            [FromQuery] string provider = "paystack",
            [FromQuery] string? storeSlug = null)
        {
            // This handles redirect after payment
            // Frontend will call verify endpoint separately
            return Ok(new
            {
                message = "Payment callback received",
                reference,
                provider,
                storeSlug
            });
        }

        // ── Private Helpers ──────────────────────────────────
        private Guid GetUserId() =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<Guid?> GetStoreIdAsync()
        {
            var userId = GetUserId();
            var storeResult = await _storeService.GetMyStoreAsync(userId);
            return storeResult.Success ? storeResult.Data?.Id : null;
        }
    }
}