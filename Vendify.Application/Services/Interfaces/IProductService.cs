using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Product;

namespace Vendify.Application.Services.Interfaces
{
    public interface IProductService
    {
        Task<ApiResponse<ProductDto>> CreateProductAsync(
            CreateProductRequest request, Guid storeId);

        Task<ApiResponse<ProductDto>> GetProductByIdAsync(Guid productId);

        Task<ApiResponse<PagedProductsDto>> GetStoreProductsAsync(
            Guid storeId, ProductFilterRequest filter);

        Task<ApiResponse<PagedProductsDto>> GetPublicStoreProductsAsync(
            string storeSlug, ProductFilterRequest filter);

        Task<ApiResponse<ProductDto>> UpdateProductAsync(
            Guid productId, UpdateProductRequest request, Guid storeId);

        Task<ApiResponse<ProductDto>> AddProductImageAsync(
            Guid productId, string imageUrl, bool isPrimary, Guid storeId);

        Task<ApiResponse<ProductDto>> DeleteProductImageAsync(
            Guid productId, Guid imageId, Guid storeId);

        Task<ApiResponse<ProductDto>> AddVariantAsync(
            Guid productId, CreateVariantRequest request, Guid storeId);

        Task<ApiResponse<ProductDto>> DeleteVariantAsync(
            Guid productId, Guid variantId, Guid storeId);

        Task<ApiResponse<ProductDto>> UpdateStockAsync(
            Guid productId, UpdateStockRequest request, Guid storeId);

        Task<ApiResponse<ProductDto>> TogglePublishAsync(
            Guid productId, Guid storeId);

        Task<ApiResponse> DeleteProductAsync(
            Guid productId, Guid storeId);
    }
}