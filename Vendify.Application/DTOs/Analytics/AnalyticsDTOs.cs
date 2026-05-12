namespace Vendify.Application.DTOs.Analytics
{
    public class DashboardDto
    {
        // Overview Cards
        public decimal TotalRevenue { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueLastMonth { get; set; }
        public decimal RevenueGrowthPercent { get; set; }

        public int TotalOrders { get; set; }
        public int OrdersThisMonth { get; set; }
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }

        public int TotalProducts { get; set; }
        public int PublishedProducts { get; set; }
        public int LowStockProducts { get; set; }
        public int OutOfStockProducts { get; set; }

        public int TotalCustomers { get; set; }
        public int NewCustomersThisMonth { get; set; }

        // Charts
        public List<RevenueChartDto> RevenueChart { get; set; } = new();
        public List<OrderStatusDto> OrdersByStatus { get; set; } = new();
        public List<TopProductDto> TopProducts { get; set; } = new();
        public List<RecentOrderDto> RecentOrders { get; set; } = new();
    }

    public class RevenueChartDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    public class OrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class TopProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int SalesCount { get; set; }
        public decimal Revenue { get; set; }
        public int StockQuantity { get; set; }
    }

    public class RecentOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SalesReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<RevenueChartDto> DailyRevenue { get; set; } = new();
        public List<TopProductDto> TopProducts { get; set; } = new();
    }

    public class InventoryReportDto
    {
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public List<LowStockProductDto> LowStockProducts { get; set; } = new();
        public List<LowStockProductDto> OutOfStockProducts { get; set; } = new();
    }

    public class LowStockProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int StockQuantity { get; set; }
        public string? CategoryName { get; set; }
        public string? ImageUrl { get; set; }
    }
}