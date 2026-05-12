using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Shipping;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ShippingController : ControllerBase
    {
        private readonly IShippingService _shippingService;
        private readonly IStoreService _storeService;

        public ShippingController(
            IShippingService shippingService,
            IStoreService storeService)
        {
            _shippingService = shippingService;
            _storeService = storeService;
        }

        // GET /api/v1/shipping/{storeSlug} — Public
        [HttpGet("{storeSlug}")]
        public async Task<IActionResult> GetPublicZones(string storeSlug)
        {
            var result = await _shippingService
                .GetPublicStoreZonesAsync(storeSlug);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // POST /api/v1/shipping/calculate — Public
        [HttpPost("calculate")]
        public async Task<IActionResult> Calculate(
            [FromBody] CalculateShippingRequest request)
        {
            var result = await _shippingService
                .CalculateShippingAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/v1/shipping/my-zones — Auth required
        [Authorize]
        [HttpGet("my-zones")]
        public async Task<IActionResult> GetMyZones()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _shippingService
                .GetStoreZonesAsync(storeId.Value);
            return Ok(result);
        }

        // POST /api/v1/shipping — Auth required
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateZone(
            [FromBody] CreateShippingZoneRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _shippingService
                .CreateZoneAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/shipping/{id} — Auth required
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateZone(
            Guid id, [FromBody] UpdateShippingZoneRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _shippingService
                .UpdateZoneAsync(id, request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/shipping/{id} — Auth required
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteZone(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _shippingService
                .DeleteZoneAsync(id, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
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