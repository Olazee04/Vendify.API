using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Order;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IStoreService _storeService;

        public OrdersController(
            IOrderService orderService,
            IStoreService storeService)
        {
            _orderService = orderService;
            _storeService = storeService;
        }

        // POST /api/v1/orders/{storeSlug} — Public (customers place orders)
        [HttpPost("{storeSlug}")]
        public async Task<IActionResult> CreateOrder(
            string storeSlug, [FromBody] CreateOrderRequest request)
        {
            var result = await _orderService
                .CreateOrderAsync(request, storeSlug);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/v1/orders/track/{orderNumber} — Public
        [HttpGet("track/{orderNumber}")]
        public async Task<IActionResult> TrackOrder(string orderNumber)
        {
            var result = await _orderService
                .GetOrderByNumberAsync(orderNumber);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // GET /api/v1/orders — Auth required (merchant views orders)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyOrders(
            [FromQuery] OrderFilterRequest filter)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _orderService
                .GetStoreOrdersAsync(storeId.Value, filter);
            return Ok(result);
        }

        // GET /api/v1/orders/{id} — Auth required
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _orderService
                .GetOrderByIdAsync(id, storeId.Value);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // PUT /api/v1/orders/{id}/status — Auth required
        [Authorize]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid id, [FromBody] UpdateOrderStatusRequest request)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _orderService
                .UpdateOrderStatusAsync(id, request, storeId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // PUT /api/v1/orders/{id}/cancel — Auth required
        [Authorize]
        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelOrder(Guid id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId == null)
                return BadRequest(new { message = "Store not found" });

            var result = await _orderService
                .CancelOrderAsync(id, storeId.Value);
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