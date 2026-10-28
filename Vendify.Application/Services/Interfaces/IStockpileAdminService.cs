using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Stockpile;

namespace Vendify.Application.Services.Interfaces
{
    public interface IStockpileAdminService
    {
        Task<ApiResponse<MerchantStockpileListDto>> ListAsync(
            Guid storeId, string? status);

        Task<ApiResponse<MerchantStockpileDetailDto>> GetAsync(
            Guid storeId, Guid stockpileId);

        Task<ApiResponse<MerchantStockpileDetailDto>> ExtendAsync(
            Guid storeId, Guid stockpileId, int days);

        Task<ApiResponse<MerchantStockpileDetailDto>> ReleaseAsync(
            Guid storeId, Guid stockpileId, string? note);

        Task<ApiResponse<MerchantStockpileDetailDto>> MarkRefundedAsync(
            Guid storeId, Guid depositId);
    }
}