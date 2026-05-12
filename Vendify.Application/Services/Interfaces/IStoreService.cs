using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Store;

namespace Vendify.Application.Services.Interfaces
{
    public interface IStoreService
    {
        Task<ApiResponse<StoreDto>> CreateStoreAsync(
            CreateStoreRequest request, Guid userId);

        Task<ApiResponse<StoreDto>> GetMyStoreAsync(Guid userId);

        Task<ApiResponse<StorePublicDto>> GetStoreBySlugAsync(string slug);

        Task<ApiResponse<StoreDto>> UpdateStoreAsync(
            UpdateStoreRequest request, Guid userId);

        Task<ApiResponse<StoreDto>> UpdateSlugAsync(
            UpdateStoreSlugRequest request, Guid userId);

        Task<ApiResponse<StoreDto>> UploadLogoAsync(
            Guid userId, string logoUrl);

        Task<ApiResponse<StoreDto>> UploadBannerAsync(
            Guid userId, string bannerUrl);

        Task<ApiResponse<SlugCheckDto>> CheckSlugAvailabilityAsync(string slug);

        Task<ApiResponse> DeleteStoreAsync(Guid userId);
    }
}