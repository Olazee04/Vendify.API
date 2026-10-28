using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Stockpile;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/stockpile-admin")]
    [Authorize]
    public class StockpileAdminController : ControllerBase
    {
        private readonly IStockpileAdminService _admin;
        private readonly IStoreService _storeService;

        public StockpileAdminController(
            IStockpileAdminService admin,
            IStoreService storeService)
        {
            _admin = admin;
            _storeService = storeService;
        }

        // GET /api/v1/stockpile-admin?status=Open
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _admin.ListAsync(storeId.Value, status);
            return Ok(result);
        }

        // GET /api/v1/stockpile-admin/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _admin.GetAsync(storeId.Value, id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // POST /api/v1/stockpile-admin/{id}/extend
        [HttpPost("{id:guid}/extend")]
        public async Task<IActionResult> Extend(
            Guid id, [FromBody] ExtendStockpileRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _admin.ExtendAsync(
                storeId.Value, id, request.Days);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/stockpile-admin/{id}/release
        [HttpPost("{id:guid}/release")]
        public async Task<IActionResult> Release(
            Guid id, [FromBody] ReleaseStockpileRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _admin.ReleaseAsync(
                storeId.Value, id, request?.Note);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/stockpile-admin/deposits/{depositId}/refunded
        [HttpPost("deposits/{depositId:guid}/refunded")]
        public async Task<IActionResult> MarkRefunded(Guid depositId)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _admin.MarkRefundedAsync(
                storeId.Value, depositId);
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