using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Stockpile;

namespace Vendify.Application.Services.Interfaces
{
    public interface IStockpileService
    {
        // Merchant
        Task<ApiResponse<StockpileSettingsDto>> GetSettingsAsync(
            Guid storeId);

        Task<ApiResponse<StockpileSettingsDto>> UpdateSettingsAsync(
            Guid storeId, UpdateStockpileSettingsRequest request);

        // Customer: adding to a stockpile
        Task<ApiResponse<StockpilePublicInfoDto>> GetPublicInfoAsync(
            string storeSlug);

        Task<ApiResponse<StockpileCheckDto>> CheckAsync(
            string storeSlug, StockpileCheckRequest request);

        Task<ApiResponse<StockpileDepositResultDto>> DepositAsync(
            string storeSlug, StockpileDepositRequest request);

        // Customer: viewing and checking out
        Task<ApiResponse<bool>> RequestAccessAsync(
            string storeSlug, StockpileAccessRequest request);

        Task<ApiResponse<StockpileTokenDto>> VerifyAccessAsync(
            string storeSlug, StockpileVerifyRequest request);

        Task<ApiResponse<StockpileViewDto>> GetMineAsync(
            string storeSlug, StockpileViewRequest request);

        Task<ApiResponse<StockpileCheckoutResultDto>> CheckoutAsync(
            string storeSlug, StockpileCheckoutRequest request);
    }
}