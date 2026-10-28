using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Payments;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class PaymentAttentionService : IPaymentAttentionService
    {
        private const string Pending = "Pending";
        private const string Processing = "Processing";
        private const string Fulfilled = "Fulfilled";
        private const string Failed = "Failed";
        private const string Resolved = "Resolved";

        private readonly VendifyDbContext _context;
        private readonly IPaymentFulfilmentService _fulfilment;

        public PaymentAttentionService(
            VendifyDbContext context,
            IPaymentFulfilmentService fulfilment)
        {
            _context = context;
            _fulfilment = fulfilment;
        }

        // ═══════════════ LIST ════════════════════════════

        public async Task<ApiResponse<PaymentAttentionListDto>> ListAsync(
            Guid storeId, bool resolved)
        {
            var stuck = DateTime.UtcNow.AddMinutes(-5);

            // "Open" = could not be recorded, still being retried,
            // or stuck part-way. Payments never paid are NOT included.
            var open = _context.PaymentIntents
                .AsNoTracking()
                .Where(i => i.StoreId == storeId &&
                    (i.Status == Failed ||
                     (i.Status == Pending && i.Attempts > 0) ||
                     (i.Status == Processing && i.UpdatedAt < stuck)));

            var openCount = await open.CountAsync();

            var rows = resolved
                ? await _context.PaymentIntents
                    .AsNoTracking()
                    .Where(i => i.StoreId == storeId && i.Status == Resolved)
                    .OrderByDescending(i => i.UpdatedAt)
                    .Take(100)
                    .ToListAsync()
                : await open
                    .OrderByDescending(i => i.UpdatedAt)
                    .Take(100)
                    .ToListAsync();

            return ApiResponse<PaymentAttentionListDto>.SuccessResponse(
                new PaymentAttentionListDto
                {
                    Items = rows.Select(Map).ToList(),
                    OpenCount = openCount
                });
        }

        // ═══════════════ TRY AGAIN ═══════════════════════

        public async Task<ApiResponse<bool>> RetryAsync(
            Guid storeId, Guid intentId)
        {
            var intent = await _context.PaymentIntents
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.Id == intentId && i.StoreId == storeId);

            if (intent == null)
                return ApiResponse<bool>.FailureResponse("Payment not found");

            if (intent.Status == Fulfilled)
                return ApiResponse<bool>.FailureResponse(
                    "This payment is already recorded.");

            if (intent.Status == Resolved)
                return ApiResponse<bool>.FailureResponse(
                    "This payment was marked as resolved.");

            // Give it a fresh start, then ask the provider again.
            // Recording is safe to repeat: it checks the payment with
            // Paystack or Flutterwave and never creates a duplicate.
            await _context.PaymentIntents
                .Where(i => i.Id == intentId && i.StoreId == storeId &&
                    (i.Status == Failed || i.Status == Pending ||
                     i.Status == Processing))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, Pending)
                    .SetProperty(x => x.Attempts, 0)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));

            var r = await _fulfilment.FulfilAsync(intent.Reference);

            return r.Success
                ? ApiResponse<bool>.SuccessResponse(true, r.Message)
                : ApiResponse<bool>.FailureResponse(r.Message);
        }

        // ═══════════════ MARK AS RESOLVED ════════════════

        public async Task<ApiResponse<bool>> ResolveAsync(
            Guid storeId, Guid intentId, string? note)
        {
            var intent = await _context.PaymentIntents
                .FirstOrDefaultAsync(i =>
                    i.Id == intentId && i.StoreId == storeId);

            if (intent == null)
                return ApiResponse<bool>.FailureResponse("Payment not found");

            if (intent.Status == Fulfilled)
                return ApiResponse<bool>.FailureResponse(
                    "This payment is already recorded, so there is nothing to resolve.");

            var cleaned = string.IsNullOrWhiteSpace(note)
                ? "Marked as resolved by the store"
                : "Resolved: " + note.Trim();

            if (!string.IsNullOrWhiteSpace(intent.FailureReason))
                cleaned += " (was: " + intent.FailureReason + ")";
            if (cleaned.Length > 500)
                cleaned = cleaned.Substring(0, 500);

            intent.Status = Resolved;
            intent.FailureReason = cleaned;
            intent.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "Marked as resolved.");
        }

        // ═══════════════ HELPERS ═════════════════════════

        private static string? Text(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) &&
            v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static PaymentAttentionItemDto Map(PaymentIntent i)
        {
            string? name = null, email = null, phone = null;
            var count = 0;
            var provider = "Paystack";

            // Read what the customer told us before paying
            try
            {
                using var doc = JsonDocument.Parse(i.PayloadJson);
                var root = doc.RootElement;

                name = Text(root, "customerName");
                email = Text(root, "customerEmail");
                phone = Text(root, "customerPhone");

                if (root.TryGetProperty("shippingAddress", out var addr) &&
                    addr.ValueKind == JsonValueKind.Object)
                {
                    name ??= Text(addr, "fullName");
                    phone ??= Text(addr, "phoneNumber");
                }

                if (root.TryGetProperty("items", out var list) &&
                    list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in list.EnumerateArray())
                    {
                        if (item.TryGetProperty("quantity", out var q) &&
                            q.TryGetInt32(out var n))
                            count += n;
                    }
                }

                if (root.TryGetProperty("paymentMethod", out var pm) &&
                    pm.ValueKind == JsonValueKind.Number &&
                    pm.TryGetInt32(out var code) && code == 1)
                    provider = "Flutterwave";
            }
            catch (JsonException)
            {
                // Show the row anyway, just without the customer details
            }

            var status = i.Status == Resolved ? "Resolved"
                : i.Status == Failed ? "Failed"
                : i.Status == Processing ? "Stuck"
                : "Retrying";

            return new PaymentAttentionItemDto
            {
                Id = i.Id,
                Reference = i.Reference,
                Kind = i.Kind,
                Provider = provider,
                Status = status,
                Attempts = i.Attempts,
                Reason = i.FailureReason,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                CustomerName = name,
                CustomerEmail = email,
                CustomerPhone = phone,
                ItemCount = count
            };
        }
    }
}