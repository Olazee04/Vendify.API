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
                .FirstOrDefaultAsync(p =>
                    p.Id == request.ProductId &&
                    p.StoreId == storeId);

            if (product == null)
                return ApiResponse<InventoryItemDto>.FailureResponse(
                    "Product not found");

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
                .Where(p =>
                    p.StoreId == storeId &&
                    p.TrackInventory &&
                    p.StockQuantity > 0 &&
                    p.StockQuantity <= threshold)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            return ApiResponse<List<InventoryItemDto>>.SuccessResponse(
                products.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<List<InventoryItemDto>>>
            GetOutOfStockItemsAsync(Guid storeId)
        {
            var products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Where(p =>
                    p.StoreId == storeId &&
                    p.TrackInventory &&
                    p.StockQuantity == 0)
                .ToListAsync();

            return ApiResponse<List<InventoryItemDto>>.SuccessResponse(
                products.Select(MapToDto).ToList());
        }

        private static InventoryItemDto MapToDto(
            Core.Entities.Product product)
        {
            var stockStatus = !product.TrackInventory
                ? "Not Tracked"
                : product.StockQuantity == 0
                    ? "Out of Stock"
                    : product.StockQuantity <= 5
                        ? "Low Stock"
                        : "In Stock";

            return new InventoryItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                CurrentStock = product.StockQuantity,
                TrackInventory = product.TrackInventory,
                StockStatus = stockStatus,
                ImageUrl = product.Images
                    .FirstOrDefault(i => i.IsPrimary)?.Url
                    ?? product.Images.FirstOrDefault()?.Url,
                Price = product.Price,
                CategoryName = product.Category?.Name
            };
        }
    }
}