using System.ComponentModel.DataAnnotations;

namespace Vendify.Application.DTOs.Inventory
{
    public class AdjustStockRequest
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }
        // Positive = add stock, Negative = remove stock

        [Required]
        public string Reason { get; set; } = string.Empty;
        // "Purchase", "Return", "Damaged", "Correction"
    }

    public class BulkStockUpdateRequest
    {
        [Required]
        public List<StockUpdateItem> Items { get; set; } = new();
    }

    public class StockUpdateItem
    {
        public Guid ProductId { get; set; }
        public int NewQuantity { get; set; }
    }

    public class InventoryItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public int CurrentStock { get; set; }
        public bool TrackInventory { get; set; }
        public string StockStatus { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public string? CategoryName { get; set; }
    }
}