using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Stockpile;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class StockpileController : ControllerBase
    {
        private readonly IStockpileService _stockpileService;
        private readonly IStoreService _storeService;

        public StockpileController(
            IStockpileService stockpileService,
            IStoreService storeService)
        {
            _stockpileService = stockpileService;
            _storeService = storeService;
        }

        // ── Merchant (login required) ────────────────────

        // GET /api/v1/stockpile/settings
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _stockpileService
                .GetSettingsAsync(storeId.Value);
            return Ok(result);
        }

        // PUT /api/v1/stockpile/settings
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings(
            [FromBody] UpdateStockpileSettingsRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _stockpileService
                .UpdateSettingsAsync(storeId.Value, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Customer: adding (public, no login) ──────────

        // GET /api/v1/stockpile/public/{storeSlug}
        [AllowAnonymous]
        [HttpGet("public/{storeSlug}")]
        public async Task<IActionResult> GetPublicInfo(string storeSlug)
        {
            var result = await _stockpileService
                .GetPublicInfoAsync(storeSlug);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // POST /api/v1/stockpile/check/{storeSlug}
        [AllowAnonymous]
        [HttpPost("check/{storeSlug}")]
        public async Task<IActionResult> Check(
            string storeSlug, [FromBody] StockpileCheckRequest request)
        {
            var result = await _stockpileService
                .CheckAsync(storeSlug, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/stockpile/deposit/{storeSlug}
        [AllowAnonymous]
        [HttpPost("deposit/{storeSlug}")]
        public async Task<IActionResult> Deposit(
            string storeSlug, [FromBody] StockpileDepositRequest request)
        {
            var result = await _stockpileService
                .DepositAsync(storeSlug, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Customer: viewing and checking out ───────────

        // POST /api/v1/stockpile/access/request/{storeSlug}
        [AllowAnonymous]
        [HttpPost("access/request/{storeSlug}")]
        public async Task<IActionResult> RequestAccess(
            string storeSlug, [FromBody] StockpileAccessRequest request)
        {
            var result = await _stockpileService
                .RequestAccessAsync(storeSlug, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/stockpile/access/verify/{storeSlug}
        [AllowAnonymous]
        [HttpPost("access/verify/{storeSlug}")]
        public async Task<IActionResult> VerifyAccess(
            string storeSlug, [FromBody] StockpileVerifyRequest request)
        {
            var result = await _stockpileService
                .VerifyAccessAsync(storeSlug, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/stockpile/mine/{storeSlug}
        [AllowAnonymous]
        [HttpPost("mine/{storeSlug}")]
        public async Task<IActionResult> GetMine(
            string storeSlug, [FromBody] StockpileViewRequest request)
        {
            var result = await _stockpileService
                .GetMineAsync(storeSlug, request);

            if (result.Success) return Ok(result);
            if (result.Message == StockpileMessages.SessionExpired)
                return Unauthorized(result);
            return BadRequest(result);
        }

        // POST /api/v1/stockpile/checkout/{storeSlug}
        [AllowAnonymous]
        [HttpPost("checkout/{storeSlug}")]
        public async Task<IActionResult> Checkout(
            string storeSlug, [FromBody] StockpileCheckoutRequest request)
        {
            var result = await _stockpileService
                .CheckoutAsync(storeSlug, request);

            if (result.Success) return Ok(result);
            if (result.Message == StockpileMessages.SessionExpired)
                return Unauthorized(result);
            return BadRequest(result);
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