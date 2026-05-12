using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Analytics;

namespace Vendify.Application.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<ApiResponse<DashboardDto>> GetDashboardAsync(Guid storeId);

        Task<ApiResponse<SalesReportDto>> GetSalesReportAsync(
            Guid storeId, DateTime fromDate, DateTime toDate);

        Task<ApiResponse<InventoryReportDto>> GetInventoryReportAsync(
            Guid storeId);
    }
}