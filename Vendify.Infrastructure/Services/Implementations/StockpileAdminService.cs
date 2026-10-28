using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Stockpile;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class StockpileAdminService : IStockpileAdminService
    {
        private const string StatusOpen = "Open";
        private const string StatusReleasing = "Releasing";
        private const string StatusReleased = "Released";

        private readonly VendifyDbContext _context;
        private readonly ILogger<StockpileAdminService> _logger;

        public StockpileAdminService(
            VendifyDbContext context,
            ILogger<StockpileAdminService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ═══════════════ LIST ════════════════════════════

        public async Task<ApiResponse<MerchantStockpileListDto>> ListAsync(
            Guid storeId, string? status)
        {
            var now = DateTime.UtcNow;

            var stockpiles = await _context.Stockpiles
                .AsNoTracking()
                .Where(s => s.StoreId == storeId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(500)
                .ToListAsync();

            var ids = stockpiles.Select(s => s.Id).ToList();

            var items = await _context.StockpileItems
                .AsNoTracking()
                .Where(i => ids.Contains(i.StockpileId))
                .ToListAsync();

            var orderIds = stockpiles
                .Where(s => s.OrderId.HasValue)
                .Select(s => s.OrderId!.Value)
                .ToList();

            var orderNumbers = await _context.Orders
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.OrderNumber);

            var rows = stockpiles.Select(s =>
            {
                var mine = items.Where(i => i.StockpileId == s.Id).ToList();
                string? orderNumber = null;
                if (s.OrderId.HasValue)
                    orderNumbers.TryGetValue(s.OrderId.Value, out orderNumber);
                return MapRow(s, mine, orderNumber, now);
            }).ToList();

            var summary = new MerchantStockpileSummaryDto
            {
                OpenCount = rows.Count(r => r.Status == "Open"),
                ExpiredCount = rows.Count(r => r.Status == "Expired"),
                CheckedOutCount = rows.Count(r => r.Status == "CheckedOut"),
                ReleasedCount = rows.Count(r => r.Status == "Released"),
                AmountHeld = rows
                    .Where(r => r.Status == "Open" || r.Status == "Expired")
                    .Sum(r => r.TotalAmount)
            };

            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                rows = rows
                    .Where(r => r.Status.Equals(
                        status, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return ApiResponse<MerchantStockpileListDto>.SuccessResponse(
                new MerchantStockpileListDto
                {
                    Summary = summary,
                    Stockpiles = rows
                });
        }

        // ═══════════════ DETAIL ══════════════════════════

        public async Task<ApiResponse<MerchantStockpileDetailDto>> GetAsync(
            Guid storeId, Guid stockpileId)
        {
            var s = await _context.Stockpiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == stockpileId && x.StoreId == storeId);

            if (s == null)
                return Fail<MerchantStockpileDetailDto>("Stockpile not found");

            var items = await _context.StockpileItems
                .AsNoTracking()
                .Where(i => i.StockpileId == stockpileId)
                .OrderBy(i => i.CreatedAt)
                .ToListAsync();

            var deposits = await _context.StockpileDeposits
                .AsNoTracking()
                .Where(d => d.StockpileId == stockpileId)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync();

            string? orderNumber = null;
            if (s.OrderId.HasValue)
            {
                orderNumber = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.Id == s.OrderId.Value)
                    .Select(o => o.OrderNumber)
                    .FirstOrDefaultAsync();
            }

            var now = DateTime.UtcNow;
            var row = MapRow(s, items, orderNumber, now);

            var detail = new MerchantStockpileDetailDto
            {
                Id = row.Id,
                CustomerName = row.CustomerName,
                CustomerEmail = row.CustomerEmail,
                CustomerPhone = row.CustomerPhone,
                Status = row.Status,
                TotalItems = row.TotalItems,
                TotalAmount = row.TotalAmount,
                TotalWeightKg = row.TotalWeightKg,
                ExpiresAt = row.ExpiresAt,
                CreatedAt = row.CreatedAt,
                OrderNumber = row.OrderNumber,
                ReleaseNote = s.ReleaseNote,
                ReleasedAt = s.ReleasedAt,
                CheckedOutAt = s.CheckedOutAt,
                Items = items.Select(i => new StockpileViewItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    ProductImageUrl = i.ProductImageUrl,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    WeightKg = i.WeightKg
                }).ToList(),
                Deposits = deposits.Select(d => new MerchantDepositDto
                {
                    Id = d.Id,
                    PaymentReference = d.PaymentReference,
                    PaymentMethod = d.PaymentMethod.ToString(),
                    AmountPaid = d.AmountPaid,
                    CreatedAt = d.CreatedAt,
                    RefundedAt = d.RefundedAt
                }).ToList()
            };

            return ApiResponse<MerchantStockpileDetailDto>.SuccessResponse(
                detail);
        }

        // ═══════════════ EXTEND ══════════════════════════

        public async Task<ApiResponse<MerchantStockpileDetailDto>> ExtendAsync(
            Guid storeId, Guid stockpileId, int days)
        {
            if (days < 1 || days > 365)
                return Fail<MerchantStockpileDetailDto>(
                    "Days must be between 1 and 365.");

            var s = await _context.Stockpiles
                .FirstOrDefaultAsync(x =>
                    x.Id == stockpileId && x.StoreId == storeId);

            if (s == null)
                return Fail<MerchantStockpileDetailDto>("Stockpile not found");

            if (s.Status != StatusOpen)
                return Fail<MerchantStockpileDetailDto>(
                    "Only open stockpiles can be extended.");

            if (!s.ExpiresAt.HasValue)
                return Fail<MerchantStockpileDetailDto>(
                    "This stockpile has no expiry date, so there is nothing to extend.");

            var now = DateTime.UtcNow;
            var from = s.ExpiresAt.Value > now ? s.ExpiresAt.Value : now;

            s.ExpiresAt = from.AddDays(days);
            s.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return await GetAsync(storeId, stockpileId);
        }

        // ═══════════════ RELEASE ═════════════════════════

        public async Task<ApiResponse<MerchantStockpileDetailDto>> ReleaseAsync(
            Guid storeId, Guid stockpileId, string? note)
        {
            var exists = await _context.Stockpiles
                .AsNoTracking()
                .AnyAsync(x => x.Id == stockpileId && x.StoreId == storeId);

            if (!exists)
                return Fail<MerchantStockpileDetailDto>("Stockpile not found");

            // Claim it first, so the customer cannot check out at the
            // same moment we are releasing it.
            var claimed = await _context.Stockpiles
                .Where(x => x.Id == stockpileId &&
                            x.StoreId == storeId &&
                            x.Status == StatusOpen)
                .ExecuteUpdateAsync(x =>
                    x.SetProperty(p => p.Status, StatusReleasing));

            if (claimed == 0)
                return Fail<MerchantStockpileDetailDto>(
                    "Only open stockpiles can be released. " +
                    "It may have just been checked out by the customer.");

            try
            {
                var s = await _context.Stockpiles
                    .FirstAsync(x => x.Id == stockpileId);

                var items = await _context.StockpileItems
                    .Where(i => i.StockpileId == stockpileId)
                    .ToListAsync();

                var productIds = items.Select(i => i.ProductId)
                    .Distinct().ToList();

                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();

                // Put the reserved stock back on the shelf
                foreach (var item in items)
                {
                    var p = products.FirstOrDefault(x => x.Id == item.ProductId);
                    if (p != null && p.TrackInventory)
                        p.StockQuantity += item.Quantity;
                }

                var cleanNote = string.IsNullOrWhiteSpace(note)
                    ? null
                    : (note.Trim().Length > 500
                        ? note.Trim().Substring(0, 500)
                        : note.Trim());

                var now = DateTime.UtcNow;
                s.Status = StatusReleased;
                s.ReleasedAt = now;
                s.ReleaseNote = cleanNote;
                s.UpdatedAt = now;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Releasing stockpile {Id} failed", stockpileId);

                _context.ChangeTracker.Clear();
                await _context.Stockpiles
                    .Where(x => x.Id == stockpileId)
                    .ExecuteUpdateAsync(x =>
                        x.SetProperty(p => p.Status, StatusOpen));

                return Fail<MerchantStockpileDetailDto>(
                    "Could not release this stockpile. Nothing was changed. Please try again.");
            }

            return await GetAsync(storeId, stockpileId);
        }

        // ═══════════════ MARK REFUNDED ═══════════════════

        public async Task<ApiResponse<MerchantStockpileDetailDto>>
            MarkRefundedAsync(Guid storeId, Guid depositId)
        {
            var deposit = await _context.StockpileDeposits
                .FirstOrDefaultAsync(d => d.Id == depositId);

            if (deposit == null)
                return Fail<MerchantStockpileDetailDto>("Payment not found");

            var s = await _context.Stockpiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == deposit.StockpileId && x.StoreId == storeId);

            if (s == null)
                return Fail<MerchantStockpileDetailDto>("Payment not found");

            if (s.Status != StatusReleased)
                return Fail<MerchantStockpileDetailDto>(
                    "Release the stockpile before marking payments as refunded.");

            if (deposit.RefundedAt == null)
            {
                deposit.RefundedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return await GetAsync(storeId, s.Id);
        }

        // ═══════════════ HELPERS ═════════════════════════

        private static ApiResponse<T> Fail<T>(string message) =>
            ApiResponse<T>.FailureResponse(message);

        private static MerchantStockpileListItemDto MapRow(
            Stockpile s,
            List<StockpileItem> items,
            string? orderNumber,
            DateTime now)
        {
            var status = s.Status == StatusOpen &&
                         s.ExpiresAt.HasValue &&
                         s.ExpiresAt.Value < now
                ? "Expired"
                : s.Status;

            return new MerchantStockpileListItemDto
            {
                Id = s.Id,
                CustomerName = s.CustomerName,
                CustomerEmail = s.CustomerEmail,
                CustomerPhone = s.CustomerPhone,
                Status = status,
                TotalItems = items.Sum(i => i.Quantity),
                TotalAmount = items.Sum(i => i.TotalPrice),
                TotalWeightKg = items.Sum(i => (i.WeightKg ?? 0m) * i.Quantity),
                ExpiresAt = s.ExpiresAt,
                CreatedAt = s.CreatedAt,
                OrderNumber = orderNumber
            };
        }
    }
}