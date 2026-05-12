using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Inventory;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly IStoreService _storeService;

        public InventoryController(
            IInventoryService inventoryService,
            IStoreService storeService)
        {
            _inventoryService = inventoryService;
            _storeService = storeService;
        }

        // GET /api/v1/inventory
        [HttpGet]
        public async Task<IActionResult> GetInventory()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _inventoryService
                .GetInventoryAsync(storeId.Value);
            return Ok(result);
        }

        // GET /api/v1/inventory/low-stock?threshold=5
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStock(
            [FromQuery] int threshold = 5)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _inventoryService
                .GetLowStockItemsAsync(storeId.Value, threshold);
            return Ok(result);
        }

        // GET /api/v1/inventory/out-of-stock
        [HttpGet("out-of-stock")]
        public async Task<IActionResult> GetOutOfStock()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _inventoryService
                .GetOutOfStockItemsAsync(storeId.Value);
            return Ok(result);
        }

        // POST /api/v1/inventory/adjust
        [HttpPost("adjust")]
        public async Task<IActionResult> AdjustStock(
            [FromBody] AdjustStockRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _inventoryService
                .AdjustStockAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/inventory/bulk-update
        [HttpPut("bulk-update")]
        public async Task<IActionResult> BulkUpdateStock(
            [FromBody] BulkStockUpdateRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _inventoryService
                .BulkUpdateStockAsync(request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

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