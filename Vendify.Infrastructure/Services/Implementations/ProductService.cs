using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Product;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly VendifyDbContext _context;

        public ProductService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<ProductDto>> CreateProductAsync(
            CreateProductRequest request, Guid storeId)
        {
            // Validate category belongs to store
            if (request.CategoryId.HasValue)
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c =>
                        c.Id == request.CategoryId &&
                        c.StoreId == storeId);

                if (category == null)
                    return ApiResponse<ProductDto>.FailureResponse(
                        "Category not found");
            }

            var product = new Product
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                Price = request.Price,
                CompareAtPrice = request.CompareAtPrice,
                StockQuantity = request.StockQuantity,
                TrackInventory = request.TrackInventory,
                IsDigital = request.IsDigital,
                DigitalFileUrl = request.DigitalFileUrl,
                Type = request.Type,
                SKU = request.SKU,
                Barcode = request.Barcode,
                Weight = request.Weight,
                WeightUnit = request.WeightUnit,
                IsPublished = request.IsPublished,
                Tags = request.Tags,
                CategoryId = request.CategoryId,
                StoreId = storeId
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Add variants if provided
            if (request.Variants != null && request.Variants.Any())
            {
                var variants = request.Variants.Select(v => new ProductVariant
                {
                    Name = v.Name,
                    Value = v.Value,
                    PriceModifier = v.PriceModifier,
                    StockQuantity = v.StockQuantity,
                    SKU = v.SKU,
                    ProductId = product.Id
                }).ToList();

                _context.ProductVariants.AddRange(variants);
                await _context.SaveChangesAsync();
            }

            var created = await GetFullProductAsync(product.Id);
            return ApiResponse<ProductDto>.SuccessResponse(
                created!, "Product created successfully");
        }

        public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(
            Guid productId)
        {
            var product = await GetFullProductAsync(productId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            return ApiResponse<ProductDto>.SuccessResponse(product);
        }

        public async Task<ApiResponse<PagedProductsDto>> GetStoreProductsAsync(
            Guid storeId, ProductFilterRequest filter)
        {
            var query = _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Category)
                .Where(p => p.StoreId == storeId);

            query = ApplyFilters(query, filter);

            var totalCount = await query.CountAsync();
            var products = await ApplyPaging(query, filter).ToListAsync();

            return ApiResponse<PagedProductsDto>.SuccessResponse(
                BuildPagedResult(products, totalCount, filter));
        }

        public async Task<ApiResponse<PagedProductsDto>> GetPublicStoreProductsAsync(
            string storeSlug, ProductFilterRequest filter)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());

            if (store == null)
                return ApiResponse<PagedProductsDto>.FailureResponse(
                    "Store not found");

            var query = _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Category)
                .Where(p => p.StoreId == store.Id && p.IsPublished);

            query = ApplyFilters(query, filter);

            var totalCount = await query.CountAsync();
            var products = await ApplyPaging(query, filter).ToListAsync();

            return ApiResponse<PagedProductsDto>.SuccessResponse(
                BuildPagedResult(products, totalCount, filter));
        }

        public async Task<ApiResponse<ProductDto>> UpdateProductAsync(
            Guid productId, UpdateProductRequest request, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            if (request.Name != null) product.Name = request.Name.Trim();
            if (request.Description != null)
                product.Description = request.Description;
            if (request.Price.HasValue) product.Price = request.Price.Value;
            if (request.CompareAtPrice.HasValue)
                product.CompareAtPrice = request.CompareAtPrice;
            if (request.StockQuantity.HasValue)
                product.StockQuantity = request.StockQuantity.Value;
            if (request.TrackInventory.HasValue)
                product.TrackInventory = request.TrackInventory.Value;
            if (request.IsPublished.HasValue)
                product.IsPublished = request.IsPublished.Value;
            if (request.SKU != null) product.SKU = request.SKU;
            if (request.Tags != null) product.Tags = request.Tags;
            if (request.Weight.HasValue) product.Weight = request.Weight;
            if (request.WeightUnit != null)
                product.WeightUnit = request.WeightUnit;
            if (request.CategoryId.HasValue)
                product.CategoryId = request.CategoryId;

            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var updated = await GetFullProductAsync(product.Id);
            return ApiResponse<ProductDto>.SuccessResponse(
                updated!, "Product updated successfully");
        }

        public async Task<ApiResponse<ProductDto>> AddProductImageAsync(
            Guid productId, string imageUrl, bool isPrimary, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            // If this is primary, unset other primary images
            if (isPrimary)
            {
                var existingImages = await _context.ProductImages
                    .Where(i => i.ProductId == productId)
                    .ToListAsync();

                existingImages.ForEach(i => i.IsPrimary = false);
            }

            var image = new ProductImage
            {
                Url = imageUrl,
                IsPrimary = isPrimary,
                ProductId = productId,
                SortOrder = await _context.ProductImages
                    .CountAsync(i => i.ProductId == productId)
            };

            _context.ProductImages.Add(image);
            await _context.SaveChangesAsync();

            var updated = await GetFullProductAsync(product.Id);
            return ApiResponse<ProductDto>.SuccessResponse(
                updated!, "Image added successfully");
        }

        public async Task<ApiResponse<ProductDto>> DeleteProductImageAsync(
            Guid productId, Guid imageId, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            var image = await _context.ProductImages
                .FirstOrDefaultAsync(i =>
                    i.Id == imageId && i.ProductId == productId);

            if (image == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Image not found");

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            var updated = await GetFullProductAsync(product.Id);
            return ApiResponse<ProductDto>.SuccessResponse(
                updated!, "Image deleted successfully");
        }

        public async Task<ApiResponse<ProductDto>> AddVariantAsync(
            Guid productId, CreateVariantRequest request, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            var variant = new ProductVariant
            {
                Name = request.Name,
                Value = request.Value,
                PriceModifier = request.PriceModifier,
                StockQuantity = request.StockQuantity,
                SKU = request.SKU,
                ProductId = productId
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            var updated = await GetFullProductAsync(product.Id);
            return ApiResponse<ProductDto>.SuccessResponse(
                updated!, "Variant added successfully");
        }

        public async Task<ApiResponse<ProductDto>> DeleteVariantAsync(
            Guid productId, Guid variantId, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v =>
                    v.Id == variantId && v.ProductId == productId);

            if (variant == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Variant not found");

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            var updated = await GetFullProductAsync(product.Id);
            return ApiResponse<ProductDto>.SuccessResponse(
                updated!, "Variant deleted successfully");
        }

        public async Task<ApiResponse<ProductDto>> UpdateStockAsync(
            Guid productId, UpdateStockRequest request, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            product.StockQuantity = request.Quantity;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var updated = await GetFullProductAsync(product.Id);
            return ApiResponse<ProductDto>.SuccessResponse(
                updated!, "Stock updated successfully");
        }

        public async Task<ApiResponse<ProductDto>> TogglePublishAsync(
            Guid productId, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse<ProductDto>.FailureResponse(
                    "Product not found");

            product.IsPublished = !product.IsPublished;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var updated = await GetFullProductAsync(product.Id);
            var message = product.IsPublished ?
                "Product published successfully" :
                "Product unpublished successfully";

            return ApiResponse<ProductDto>.SuccessResponse(updated!, message);
        }

        public async Task<ApiResponse> DeleteProductAsync(
            Guid productId, Guid storeId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId && p.StoreId == storeId);

            if (product == null)
                return ApiResponse.FailureResponse("Product not found");

            product.IsDeleted = true;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse(
                "Product deleted successfully");
        }

        // ── Private Helpers ──────────────────────────────────

        private async Task<ProductDto?> GetFullProductAsync(Guid productId)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return null;

            return MapToProductDto(product);
        }

        private static IQueryable<Product> ApplyFilters(
            IQueryable<Product> query, ProductFilterRequest filter)
        {
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(p =>
                    p.Name.ToLower().Contains(filter.Search.ToLower()) ||
                    (p.Description != null &&
                     p.Description.ToLower().Contains(filter.Search.ToLower())));

            if (filter.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == filter.CategoryId);

            if (filter.Type.HasValue)
                query = query.Where(p => p.Type == filter.Type);

            if (filter.IsPublished.HasValue)
                query = query.Where(p => p.IsPublished == filter.IsPublished);

            if (filter.MinPrice.HasValue)
                query = query.Where(p => p.Price >= filter.MinPrice);

            if (filter.MaxPrice.HasValue)
                query = query.Where(p => p.Price <= filter.MaxPrice);

            return filter.SortBy.ToLower() switch
            {
                "price" => filter.SortOrder == "asc"
                    ? query.OrderBy(p => p.Price)
                    : query.OrderByDescending(p => p.Price),
                "name" => filter.SortOrder == "asc"
                    ? query.OrderBy(p => p.Name)
                    : query.OrderByDescending(p => p.Name),
                "sales" => query.OrderByDescending(p => p.SalesCount),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };
        }

        private static IQueryable<Product> ApplyPaging(
            IQueryable<Product> query, ProductFilterRequest filter)
        {
            return query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize);
        }

        private static PagedProductsDto BuildPagedResult(
            List<Product> products, int totalCount, ProductFilterRequest filter)
        {
            var totalPages = (int)Math.Ceiling(
                (double)totalCount / filter.PageSize);

            return new PagedProductsDto
            {
                Products = products.Select(MapToProductDto).ToList(),
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                HasNextPage = filter.Page < totalPages,
                HasPreviousPage = filter.Page > 1
            };
        }

        private static ProductDto MapToProductDto(Product product)
        {
            decimal? discountPercentage = null;
            if (product.CompareAtPrice.HasValue &&
                product.CompareAtPrice > product.Price)
            {
                discountPercentage = Math.Round(
                    (product.CompareAtPrice.Value - product.Price) /
                    product.CompareAtPrice.Value * 100, 1);
            }

            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                CompareAtPrice = product.CompareAtPrice,
                DiscountPercentage = discountPercentage,
                StockQuantity = product.StockQuantity,
                TrackInventory = product.TrackInventory,
                IsDigital = product.IsDigital,
                DigitalFileUrl = product.IsDigital ? product.DigitalFileUrl : null,
                Type = product.Type.ToString(),
                SKU = product.SKU,
                Barcode = product.Barcode,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                IsPublished = product.IsPublished,
                Tags = product.Tags,
                SalesCount = product.SalesCount,
                StoreId = product.StoreId,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                Images = product.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        Url = i.Url,
                        AltText = i.AltText,
                        IsPrimary = i.IsPrimary,
                        SortOrder = i.SortOrder
                    }).ToList(),
                Variants = product.Variants
                    .Select(v => new ProductVariantDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Value = v.Value,
                        PriceModifier = v.PriceModifier,
                        StockQuantity = v.StockQuantity,
                        SKU = v.SKU
                    }).ToList(),
                CreatedAt = product.CreatedAt
            };
        }
    }
}