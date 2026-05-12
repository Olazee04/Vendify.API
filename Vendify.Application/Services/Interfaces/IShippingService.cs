using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Shipping;

namespace Vendify.Application.Services.Interfaces
{
    public interface IShippingService
    {
        Task<ApiResponse<ShippingZoneDto>> CreateZoneAsync(
            CreateShippingZoneRequest request, Guid storeId);

        Task<ApiResponse<List<ShippingZoneDto>>> GetStoreZonesAsync(
            Guid storeId);

        Task<ApiResponse<List<ShippingZoneDto>>> GetPublicStoreZonesAsync(
            string storeSlug);

        Task<ApiResponse<ShippingZoneDto>> UpdateZoneAsync(
            Guid zoneId, UpdateShippingZoneRequest request, Guid storeId);

        Task<ApiResponse<ShippingCalculateDto>> CalculateShippingAsync(
            CalculateShippingRequest request);

        Task<ApiResponse> DeleteZoneAsync(Guid zoneId, Guid storeId);
    }
}