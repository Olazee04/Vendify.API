using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Category;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly VendifyDbContext _context;

        public CategoryService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(
            CreateCategoryRequest request, Guid storeId)
        {
            // Check for duplicate name in store
            var exists = await _context.Categories
                .AnyAsync(c =>
                    c.StoreId == storeId &&
                    c.Name.ToLower() == request.Name.ToLower());

            if (exists)
                return ApiResponse<CategoryDto>.FailureResponse(
                    "A category with this name already exists");

            var category = new Category
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                SortOrder = request.SortOrder,
                StoreId = storeId
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return ApiResponse<CategoryDto>.SuccessResponse(
                await MapToDtoAsync(category),
                "Category created successfully");
        }

        public async Task<ApiResponse<List<CategoryDto>>>
            GetStoreCategoriesAsync(Guid storeId)
        {
            var categories = await _context.Categories
                .Where(c => c.StoreId == storeId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var dtos = new List<CategoryDto>();
            foreach (var cat in categories)
                dtos.Add(await MapToDtoAsync(cat));

            return ApiResponse<List<CategoryDto>>.SuccessResponse(dtos);
        }

        public async Task<ApiResponse<List<CategoryDto>>>
            GetPublicCategoriesAsync(string storeSlug)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s =>
                    s.Slug == storeSlug.ToLower());

            if (store == null)
                return ApiResponse<List<CategoryDto>>.FailureResponse(
                    "Store not found");

            var categories = await _context.Categories
                .Where(c => c.StoreId == store.Id)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var dtos = new List<CategoryDto>();
            foreach (var cat in categories)
                dtos.Add(await MapToDtoAsync(cat));

            return ApiResponse<List<CategoryDto>>.SuccessResponse(dtos);
        }

        public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(
            Guid categoryId, UpdateCategoryRequest request, Guid storeId)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.Id == categoryId && c.StoreId == storeId);

            if (category == null)
                return ApiResponse<CategoryDto>.FailureResponse(
                    "Category not found");

            if (request.Name != null)
            {
                // Check duplicate on rename
                var duplicate = await _context.Categories
                    .AnyAsync(c =>
                        c.StoreId == storeId &&
                        c.Name.ToLower() == request.Name.ToLower() &&
                        c.Id != categoryId);

                if (duplicate)
                    return ApiResponse<CategoryDto>.FailureResponse(
                        "A category with this name already exists");

                category.Name = request.Name.Trim();
            }

            if (request.Description != null)
                category.Description = request.Description;
            if (request.ImageUrl != null)
                category.ImageUrl = request.ImageUrl;
            if (request.SortOrder.HasValue)
                category.SortOrder = request.SortOrder.Value;

            category.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<CategoryDto>.SuccessResponse(
                await MapToDtoAsync(category),
                "Category updated successfully");
        }

        public async Task<ApiResponse> DeleteCategoryAsync(
            Guid categoryId, Guid storeId)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.Id == categoryId && c.StoreId == storeId);

            if (category == null)
                return ApiResponse.FailureResponse("Category not found");

            // Unlink products from this category
            var products = await _context.Products
                .Where(p => p.CategoryId == categoryId)
                .ToListAsync();

            products.ForEach(p => p.CategoryId = null);

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse(
                "Category deleted successfully");
        }

        // ── Private Helpers ──────────────────────────────────

        private async Task<CategoryDto> MapToDtoAsync(Category category)
        {
            var productCount = await _context.Products
                .CountAsync(p =>
                    p.CategoryId == category.Id && !p.IsDeleted);

            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                SortOrder = category.SortOrder,
                ProductCount = productCount,
                StoreId = category.StoreId,
                CreatedAt = category.CreatedAt
            };
        }
    }
}