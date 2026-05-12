using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Product;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IStoreService _storeService;

        public ProductsController(
            IProductService productService,
            IStoreService storeService)
        {
            _productService = productService;
            _storeService = storeService;
        }

        // GET /api/v1/products/{id} — Public
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            var result = await _productService.GetProductByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // GET /api/v1/products/store/{slug} — Public
        [HttpGet("store/{slug}")]
        public async Task<IActionResult> GetPublicProducts(
            string slug, [FromQuery] ProductFilterRequest filter)
        {
            var result = await _productService
                .GetPublicStoreProductsAsync(slug, filter);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // GET /api/v1/products/my-products — Auth required
        [Authorize]
        [HttpGet("my-products")]
        public async Task<IActionResult> GetMyProducts(
            [FromQuery] ProductFilterRequest filter)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "You don't have a store yet" });

            var result = await _productService
                .GetStoreProductsAsync(storeId.Value, filter);
            return Ok(result);
        }

        // POST /api/v1/products — Auth required
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProduct(
            [FromBody] CreateProductRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "You don't have a store yet" });

            var result = await _productService
                .CreateProductAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/products/{id} — Auth required
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(
            Guid id, [FromBody] UpdateProductRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .UpdateProductAsync(id, request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/products/{id}/images — Auth required
        [Authorize]
        [HttpPost("{id}/images")]
        public async Task<IActionResult> AddImage(
            Guid id, [FromBody] AddImageRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .AddProductImageAsync(
                    id, request.ImageUrl, request.IsPrimary, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/products/{id}/images/{imageId} — Auth required
        [Authorize]
        [HttpDelete("{id}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(Guid id, Guid imageId)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .DeleteProductImageAsync(id, imageId, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // POST /api/v1/products/{id}/variants — Auth required
        [Authorize]
        [HttpPost("{id}/variants")]
        public async Task<IActionResult> AddVariant(
            Guid id, [FromBody] CreateVariantRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .AddVariantAsync(id, request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/products/{id}/variants/{variantId} — Auth required
        [Authorize]
        [HttpDelete("{id}/variants/{variantId}")]
        public async Task<IActionResult> DeleteVariant(
            Guid id, Guid variantId)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .DeleteVariantAsync(id, variantId, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/products/{id}/stock — Auth required
        [Authorize]
        [HttpPut("{id}/stock")]
        public async Task<IActionResult> UpdateStock(
            Guid id, [FromBody] UpdateStockRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .UpdateStockAsync(id, request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/products/{id}/toggle-publish — Auth required
        [Authorize]
        [HttpPut("{id}/toggle-publish")]
        public async Task<IActionResult> TogglePublish(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .TogglePublishAsync(id, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/products/{id} — Auth required
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _productService
                .DeleteProductAsync(id, storeId.Value);
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

    public class AddImageRequest
    {
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; } = false;
    }
}