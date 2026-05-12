using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IStoreService _storeService;

        public AnalyticsController(
            IAnalyticsService analyticsService,
            IStoreService storeService)
        {
            _analyticsService = analyticsService;
            _storeService = storeService;
        }

        // GET /api/v1/analytics/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _analyticsService
                .GetDashboardAsync(storeId.Value);
            return Ok(result);
        }

        // GET /api/v1/analytics/sales?fromDate=2026-01-01&toDate=2026-12-31
        [HttpGet("sales")]
        public async Task<IActionResult> GetSalesReport(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
            var to = toDate ?? DateTime.UtcNow;

            var result = await _analyticsService
                .GetSalesReportAsync(storeId.Value, from, to);
            return Ok(result);
        }

        // GET /api/v1/analytics/inventory
        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventoryReport()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _analyticsService
                .GetInventoryReportAsync(storeId.Value);
            return Ok(result);
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