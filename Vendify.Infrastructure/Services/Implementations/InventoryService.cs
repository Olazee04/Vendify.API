using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Inventory;
using Vendify.Application.Services.Interfaces;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class InventoryService : IInventoryService
    {
        private readonly VendifyDbContext _context;

        public InventoryService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<InventoryItemDto>>>
            GetInventoryAsync(Guid storeId)
        {
            var products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Where(p => p.StoreId == storeId)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            return ApiResponse<List<InventoryItemDto>>.SuccessResponse(
                products.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<InventoryItemDto>> AdjustStockAsync(
     AdjustStockRequest request, Guid storeId)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p =>
                    p.Id == request.ProductId &&
                    p.StoreId == storeId);

            if (product == null)
                return ApiResponse<InventoryItemDto>.FailureResponse(
                    "Product not found");

            if (product.Variants.Any())
            {
                if (!request.VariantId.HasValue)
                    return ApiResponse<InventoryItemDto>.FailureResponse(
                        "Choose which option to adjust.");

                var variant = product.Variants
                    .FirstOrDefault(v => v.Id == request.VariantId.Value);
                if (variant == null)
                    return ApiResponse<InventoryItemDto>.FailureResponse(
                        "That option was not found.");

                var newVariantQty = variant.StockQuantity + request.Quantity;
                if (newVariantQty < 0)
                    return ApiResponse<InventoryItemDto>.FailureResponse(
                        $"Cannot reduce stock below 0. " +
                        $"Current stock: {variant.StockQuantity}");

                variant.StockQuantity = newVariantQty;
                // Keep the product total equal to the sum of its options
                product.StockQuantity =
                    product.Variants.Sum(v => v.StockQuantity);
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<InventoryItemDto>.SuccessResponse(
                    MapToDto(product),
                    $"Stock adjusted. New quantity: {newVariantQty}");
            }

            var newQuantity = product.StockQuantity + request.Quantity;

            if (newQuantity < 0)
                return ApiResponse<InventoryItemDto>.FailureResponse(
                    $"Cannot reduce stock below 0. " +
                    $"Current stock: {product.StockQuantity}");

            product.StockQuantity = newQuantity;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<InventoryItemDto>.SuccessResponse(
                MapToDto(product),
                $"Stock adjusted successfully. " +
                $"New quantity: {newQuantity}");
        }

        public async Task<ApiResponse> BulkUpdateStockAsync(
            BulkStockUpdateRequest request, Guid storeId)
        {
            var productIds = request.Items.Select(i => i.ProductId).ToList();

            var products = await _context.Products
                .Where(p =>
                    productIds.Contains(p.Id) &&
                    p.StoreId == storeId)
                .ToListAsync();

            foreach (var item in request.Items)
            {
                var product = products
                    .FirstOrDefault(p => p.Id == item.ProductId);

                if (product != null)
                {
                    product.StockQuantity = item.NewQuantity;
                    product.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse(
                $"Updated stock for {products.Count} products");
        }

        public async Task<ApiResponse<List<InventoryItemDto>>>
     GetLowStockItemsAsync(Guid storeId, int threshold = 5)
        {
            var products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Where(p => p.StoreId == storeId && p.TrackInventory)
                .ToListAsync();

            var items = products
                .Select(p => MapToDto(p, threshold))
                .Where(i => i.StockStatus == "Low Stock")
                .OrderBy(i => i.CurrentStock)
                .ToList();

            return ApiResponse<List<InventoryItemDto>>.SuccessResponse(items);
        }

        public async Task<ApiResponse<List<InventoryItemDto>>>
            GetOutOfStockItemsAsync(Guid storeId)
        {
            var products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Where(p => p.StoreId == storeId && p.TrackInventory)
                .ToListAsync();

            var items = products
                .Select(p => MapToDto(p))
                .Where(i => i.StockStatus == "Out of Stock")
                .ToList();

            return ApiResponse<List<InventoryItemDto>>.SuccessResponse(items);
        }

        private static InventoryItemDto MapToDto(
     Core.Entities.Product product, int lowAt = 5)
        {
            var hasOptions = product.Variants.Any();
            var total = hasOptions
                ? product.Variants.Sum(v => v.StockQuantity)
                : product.StockQuantity;

            string StatusOf(int n) =>
                n <= 0 ? "Out of Stock" : n <= lowAt ? "Low Stock" : "In Stock";

            string stockStatus;
            if (!product.TrackInventory) stockStatus = "Not Tracked";
            else if (!hasOptions) stockStatus = StatusOf(product.StockQuantity);
            else if (total <= 0) stockStatus = "Out of Stock";
            else if (product.Variants.Any(v => v.StockQuantity <= lowAt))
                stockStatus = "Low Stock";
            else stockStatus = "In Stock";

            return new InventoryItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                CurrentStock = total,
                TrackInventory = product.TrackInventory,
                StockStatus = stockStatus,
                ImageUrl = product.Images
                    .FirstOrDefault(i => i.IsPrimary)?.Url
                    ?? product.Images.FirstOrDefault()?.Url,
                Price = product.Price,
                CategoryName = product.Category?.Name,
                Variants = product.Variants.Select(v => new InventoryVariantDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Value = v.Value,
                    StockQuantity = v.StockQuantity,
                    StockStatus = StatusOf(v.StockQuantity)
                }).ToList()
            };
        }
    }
}
    
