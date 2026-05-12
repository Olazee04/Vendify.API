using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Coupon;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class CouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;
        private readonly IStoreService _storeService;

        public CouponsController(
            ICouponService couponService,
            IStoreService storeService)
        {
            _couponService = couponService;
            _storeService = storeService;
        }

        // POST /api/v1/coupons/validate — Public
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateCoupon(
            [FromBody] ValidateCouponRequest request)
        {
            var result = await _couponService.ValidateCouponAsync(request);
            return Ok(result);
        }

        // GET /api/v1/coupons — Auth required
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyCoupons()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _couponService
                .GetStoreCouponsAsync(storeId.Value);
            return Ok(result);
        }

        // GET /api/v1/coupons/{id} — Auth required
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCoupon(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _couponService
                .GetCouponByIdAsync(id, storeId.Value);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // POST /api/v1/coupons — Auth required
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateCoupon(
            [FromBody] CreateCouponRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _couponService
                .CreateCouponAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/coupons/{id} — Auth required
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCoupon(
            Guid id, [FromBody] UpdateCouponRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _couponService
                .UpdateCouponAsync(id, request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/coupons/{id}/toggle — Auth required
        [Authorize]
        [HttpPut("{id}/toggle")]
        public async Task<IActionResult> ToggleCoupon(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _couponService
                .ToggleCouponAsync(id, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/coupons/{id} — Auth required
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCoupon(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _couponService
                .DeleteCouponAsync(id, storeId.Value);
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