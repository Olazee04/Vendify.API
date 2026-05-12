using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Analytics;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly VendifyDbContext _context;

        public AnalyticsService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<DashboardDto>> GetDashboardAsync(
            Guid storeId)
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(
                now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddSeconds(-1);

            // ── Revenue ──────────────────────────────────────
            var allPaidOrders = await _context.Orders
                .Where(o =>
                    o.StoreId == storeId &&
                    o.PaymentStatus == PaymentStatus.Paid)
                .ToListAsync();

            var totalRevenue = allPaidOrders.Sum(o => o.Total);

            var revenueThisMonth = allPaidOrders
                .Where(o => o.CreatedAt >= startOfMonth)
                .Sum(o => o.Total);

            var revenueLastMonth = allPaidOrders
                .Where(o =>
                    o.CreatedAt >= startOfLastMonth &&
                    o.CreatedAt <= endOfLastMonth)
                .Sum(o => o.Total);

            var revenueGrowth = revenueLastMonth > 0
                ? Math.Round(
                    (revenueThisMonth - revenueLastMonth) /
                    revenueLastMonth * 100, 1)
                : 0;

            // ── Orders ───────────────────────────────────────
            var allOrders = await _context.Orders
                .Where(o => o.StoreId == storeId)
                .ToListAsync();

            var ordersThisMonth = allOrders
                .Count(o => o.CreatedAt >= startOfMonth);

            var pendingOrders = allOrders
                .Count(o => o.Status == OrderStatus.Pending);

            var processingOrders = allOrders
                .Count(o => o.Status == OrderStatus.Processing);

            // ── Products ─────────────────────────────────────
            var allProducts = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Where(p => p.StoreId == storeId)
                .ToListAsync();

            var publishedProducts = allProducts.Count(p => p.IsPublished);
            var lowStockProducts = allProducts
                .Count(p => p.TrackInventory &&
                    p.StockQuantity > 0 && p.StockQuantity <= 5);
            var outOfStockProducts = allProducts
                .Count(p => p.TrackInventory && p.StockQuantity == 0);

            // ── Customers ────────────────────────────────────
            var uniqueEmails = allOrders
                .Select(o => o.CustomerEmail.ToLower())
                .Distinct()
                .Count();

            var newCustomersThisMonth = allOrders
                .Where(o => o.CreatedAt >= startOfMonth)
                .Select(o => o.CustomerEmail.ToLower())
                .Distinct()
                .Count();

            // ── Revenue Chart (last 6 months) ────────────────
            var revenueChart = new List<RevenueChartDto>();
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = new DateTime(
                    now.Year, now.Month, 1,
                    0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1).AddSeconds(-1);

                var monthOrders = allPaidOrders
                    .Where(o =>
                        o.CreatedAt >= monthStart &&
                        o.CreatedAt <= monthEnd)
                    .ToList();

                revenueChart.Add(new RevenueChartDto
                {
                    Label = monthStart.ToString("MMM yyyy"),
                    Revenue = monthOrders.Sum(o => o.Total),
                    Orders = monthOrders.Count
                });
            }

            // ── Orders By Status ─────────────────────────────
            var totalOrderCount = allOrders.Count;
            var ordersByStatus = allOrders
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Percentage = totalOrderCount > 0
                        ? Math.Round(
                            (decimal)g.Count() / totalOrderCount * 100, 1)
                        : 0
                })
                .OrderByDescending(o => o.Count)
                .ToList();

            // ── Top Products ─────────────────────────────────
            var orderItems = await _context.OrderItems
                .Where(oi => _context.Orders
                    .Any(o =>
                        o.Id == oi.OrderId &&
                        o.StoreId == storeId &&
                        o.PaymentStatus == PaymentStatus.Paid))
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalSales = g.Sum(i => i.Quantity),
                    TotalRevenue = g.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(5)
                .ToListAsync();

            var topProducts = new List<TopProductDto>();
            foreach (var item in orderItems)
            {
                var product = allProducts
                    .FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                {
                    topProducts.Add(new TopProductDto
                    {
                        Id = product.Id,
                        Name = product.Name,
                        ImageUrl = product.Images
                            .FirstOrDefault(i => i.IsPrimary)?.Url
                            ?? product.Images.FirstOrDefault()?.Url,
                        SalesCount = item.TotalSales,
                        Revenue = item.TotalRevenue,
                        StockQuantity = product.StockQuantity
                    });
                }
            }

            // ── Recent Orders ────────────────────────────────
            var recentOrders = allOrders
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .Select(o => new RecentOrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.CustomerName,
                    Total = o.Total,
                    Status = o.Status.ToString(),
                    PaymentStatus = o.PaymentStatus.ToString(),
                    CreatedAt = o.CreatedAt
                })
                .ToList();

            return ApiResponse<DashboardDto>.SuccessResponse(
                new DashboardDto
                {
                    TotalRevenue = totalRevenue,
                    RevenueThisMonth = revenueThisMonth,
                    RevenueLastMonth = revenueLastMonth,
                    RevenueGrowthPercent = revenueGrowth,
                    TotalOrders = allOrders.Count,
                    OrdersThisMonth = ordersThisMonth,
                    PendingOrders = pendingOrders,
                    ProcessingOrders = processingOrders,
                    TotalProducts = allProducts.Count,
                    PublishedProducts = publishedProducts,
                    LowStockProducts = lowStockProducts,
                    OutOfStockProducts = outOfStockProducts,
                    TotalCustomers = uniqueEmails,
                    NewCustomersThisMonth = newCustomersThisMonth,
                    RevenueChart = revenueChart,
                    OrdersByStatus = ordersByStatus,
                    TopProducts = topProducts,
                    RecentOrders = recentOrders
                });
        }

        public async Task<ApiResponse<SalesReportDto>> GetSalesReportAsync(
            Guid storeId, DateTime fromDate, DateTime toDate)
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o =>
                    o.StoreId == storeId &&
                    o.PaymentStatus == PaymentStatus.Paid &&
                    o.CreatedAt >= fromDate &&
                    o.CreatedAt <= toDate)
                .ToListAsync();

            var totalRevenue = orders.Sum(o => o.Total);
            var totalOrders = orders.Count;
            var avgOrderValue = totalOrders > 0
                ? totalRevenue / totalOrders : 0;

            // Daily revenue breakdown
            var dailyRevenue = orders
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RevenueChartDto
                {
                    Label = g.Key.ToString("dd MMM"),
                    Revenue = g.Sum(o => o.Total),
                    Orders = g.Count()
                })
                .ToList();

            // Top products in period
            var productSales = orders
                .SelectMany(o => o.Items)
                .GroupBy(i => new
                {
                    i.ProductId,
                    i.ProductName,
                    i.ProductImageUrl
                })
                .Select(g => new TopProductDto
                {
                    Id = g.Key.ProductId,
                    Name = g.Key.ProductName,
                    ImageUrl = g.Key.ProductImageUrl,
                    SalesCount = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(p => p.Revenue)
                .Take(10)
                .ToList();

            return ApiResponse<SalesReportDto>.SuccessResponse(
                new SalesReportDto
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    TotalRevenue = totalRevenue,
                    TotalOrders = totalOrders,
                    AverageOrderValue = avgOrderValue,
                    DailyRevenue = dailyRevenue,
                    TopProducts = productSales
                });
        }

        public async Task<ApiResponse<InventoryReportDto>>
            GetInventoryReportAsync(Guid storeId)
        {
            var products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Where(p =>
                    p.StoreId == storeId &&
                    p.TrackInventory)
                .ToListAsync();

            var lowStock = products
                .Where(p =>
                    p.StockQuantity > 0 && p.StockQuantity <= 5)
                .Select(p => new LowStockProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    StockQuantity = p.StockQuantity,
                    CategoryName = p.Category?.Name,
                    ImageUrl = p.Images
                        .FirstOrDefault(i => i.IsPrimary)?.Url
                        ?? p.Images.FirstOrDefault()?.Url
                })
                .OrderBy(p => p.StockQuantity)
                .ToList();

            var outOfStock = products
                .Where(p => p.StockQuantity == 0)
                .Select(p => new LowStockProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    StockQuantity = 0,
                    CategoryName = p.Category?.Name,
                    ImageUrl = p.Images
                        .FirstOrDefault(i => i.IsPrimary)?.Url
                        ?? p.Images.FirstOrDefault()?.Url
                })
                .ToList();

            return ApiResponse<InventoryReportDto>.SuccessResponse(
                new InventoryReportDto
                {
                    TotalProducts = products.Count,
                    LowStockCount = lowStock.Count,
                    OutOfStockCount = outOfStock.Count,
                    LowStockProducts = lowStock,
                    OutOfStockProducts = outOfStock
                });
        }
    }
}