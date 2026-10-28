using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vendify.Infrastructure.Data;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/payments-attention")]
    public class PaymentsAttentionController : ControllerBase
    {
        private readonly VendifyDbContext _context;

        public PaymentsAttentionController(VendifyDbContext context)
        {
            _context = context;
        }

        // GET /api/v1/payments-attention
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userIdText =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdText, out var userId))
                return Unauthorized();

            var storeId = await _context.Stores
                .Where(s => s.UserId == userId)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();

            if (storeId == Guid.Empty)
                return NotFound(new { success = false, message = "Store not found" });

            var intents = await _context.PaymentIntents
                .AsNoTracking()
                .Where(i => i.StoreId == storeId
                    && i.Status != "Fulfilled"
                    && (i.FailureReason != null
                        || i.Attempts > 0
                        || i.Status == "Failed"))
                .OrderByDescending(i => i.CreatedAt)
                .Take(100)
                .ToListAsync();

            var rows = intents.Select(i =>
            {
                string? name = null, email = null, phone = null;
                try
                {
                    using var doc = JsonDocument.Parse(i.PayloadJson ?? "{}");
                    var root = doc.RootElement;
                    if (root.TryGetProperty("customerName", out var n)) name = n.GetString();
                    if (root.TryGetProperty("customerEmail", out var e)) email = e.GetString();
                    if (root.TryGetProperty("customerPhone", out var p)) phone = p.GetString();
                }
                catch { }

                return new
                {
                    reference = i.Reference,
                    kind = i.Kind.ToString(),
                    status = i.Status,
                    attempts = i.Attempts,
                    failureReason = i.FailureReason,
                    createdAt = i.CreatedAt,
                    customerName = name,
                    customerEmail = email,
                    customerPhone = phone
                };
            }).ToList();

            return Ok(new { success = true, data = rows });
        }
    }
}