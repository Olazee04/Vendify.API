using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Category;

namespace Vendify.Application.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<ApiResponse<CategoryDto>> CreateCategoryAsync(
            CreateCategoryRequest request, Guid storeId);

        Task<ApiResponse<List<CategoryDto>>> GetStoreCategoriesAsync(
            Guid storeId);

        Task<ApiResponse<List<CategoryDto>>> GetPublicCategoriesAsync(
            string storeSlug);

        Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(
            Guid categoryId, UpdateCategoryRequest request, Guid storeId);

        Task<ApiResponse> DeleteCategoryAsync(
            Guid categoryId, Guid storeId);
    }
}