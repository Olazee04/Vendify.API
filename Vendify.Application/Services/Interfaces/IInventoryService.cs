using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Inventory;

namespace Vendify.Application.Services.Interfaces
{
    public interface IInventoryService
    {
        Task<ApiResponse<List<InventoryItemDto>>> GetInventoryAsync(
            Guid storeId);

        Task<ApiResponse<InventoryItemDto>> AdjustStockAsync(
            AdjustStockRequest request, Guid storeId);

        Task<ApiResponse> BulkUpdateStockAsync(
            BulkStockUpdateRequest request, Guid storeId);

        Task<ApiResponse<List<InventoryItemDto>>> GetLowStockItemsAsync(
            Guid storeId, int threshold = 5);

        Task<ApiResponse<List<InventoryItemDto>>> GetOutOfStockItemsAsync(
            Guid storeId);
    }
}