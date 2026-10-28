using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Payments;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/payments-admin")]
    [Authorize]
    public class PaymentAttentionController : ControllerBase
    {
        private readonly IPaymentAttentionService _attention;
        private readonly IStoreService _storeService;

        public PaymentAttentionController(
            IPaymentAttentionService attention,
            IStoreService storeService)
        {
            _attention = attention;
            _storeService = storeService;
        }

        // GET /api/v1/payments-admin?resolved=false
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool resolved = false)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _attention.ListAsync(storeId.Value, resolved);
            return Ok(result);
        }

        // POST /api/v1/payments-admin/{id}/retry
        [HttpPost("{id:guid}/retry")]
        public async Task<IActionResult> Retry(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _attention.RetryAsync(storeId.Value, id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/payments-admin/{id}/resolve
        [HttpPost("{id:guid}/resolve")]
        public async Task<IActionResult> Resolve(
            Guid id, [FromBody] ResolvePaymentRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _attention.ResolveAsync(
                storeId.Value, id, request?.Note);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Private Helpers ──────────────────────────────
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