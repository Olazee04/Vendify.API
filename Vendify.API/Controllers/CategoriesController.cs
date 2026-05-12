using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Category;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IStoreService _storeService;

        public CategoriesController(
            ICategoryService categoryService,
            IStoreService storeService)
        {
            _categoryService = categoryService;
            _storeService = storeService;
        }

        // GET /api/v1/categories/{storeSlug} — Public
        [HttpGet("{storeSlug}")]
        public async Task<IActionResult> GetPublicCategories(
            string storeSlug)
        {
            var result = await _categoryService
                .GetPublicCategoriesAsync(storeSlug);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // GET /api/v1/categories — Auth required
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyCategories()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _categoryService
                .GetStoreCategoriesAsync(storeId.Value);
            return Ok(result);
        }

        // POST /api/v1/categories — Auth required
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateCategory(
            [FromBody] CreateCategoryRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _categoryService
                .CreateCategoryAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/categories/{id} — Auth required
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(
            Guid id, [FromBody] UpdateCategoryRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _categoryService
                .UpdateCategoryAsync(id, request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/categories/{id} — Auth required
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _categoryService
                .DeleteCategoryAsync(id, storeId.Value);
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