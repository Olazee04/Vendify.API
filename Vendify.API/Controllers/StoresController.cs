using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Store;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class StoresController : ControllerBase
    {
        private readonly IStoreService _storeService;

        public StoresController(IStoreService storeService)
        {
            _storeService = storeService;
        }

        // GET /api/v1/stores/{slug} — Public
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _storeService.GetStoreBySlugAsync(slug);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // GET /api/v1/stores/check-slug/{slug} — Public
        [HttpGet("check-slug/{slug}")]
        public async Task<IActionResult> CheckSlug(string slug)
        {
            var result = await _storeService.CheckSlugAvailabilityAsync(slug);
            return Ok(result);
        }

        // GET /api/v1/stores/my-store — Auth required
        [Authorize]
        [HttpGet("my-store")]
        public async Task<IActionResult> GetMyStore()
        {
            var userId = GetUserId();
            var result = await _storeService.GetMyStoreAsync(userId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // POST /api/v1/stores — Auth required
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateStore(
            [FromBody] CreateStoreRequest request)
        {
            var userId = GetUserId();
            var result = await _storeService.CreateStoreAsync(request, userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/stores — Auth required
        [Authorize]
        [HttpPut]
        public async Task<IActionResult> UpdateStore(
            [FromBody] UpdateStoreRequest request)
        {
            var userId = GetUserId();
            var result = await _storeService.UpdateStoreAsync(request, userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/stores/slug — Auth required
        [Authorize]
        [HttpPut("slug")]
        public async Task<IActionResult> UpdateSlug(
            [FromBody] UpdateStoreSlugRequest request)
        {
            var userId = GetUserId();
            var result = await _storeService.UpdateSlugAsync(request, userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/stores — Auth required
        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> DeleteStore()
        {
            var userId = GetUserId();
            var result = await _storeService.DeleteStoreAsync(userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Private Helper ───────────────────────────────────
        private Guid GetUserId() =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}