namespace Vendify.Core.Entities
{
    public class ProductVariant : BaseEntity
    {
        public string Name { get; set; } = string.Empty;   // e.g. "Size", "Color"
        public string Value { get; set; } = string.Empty;  // e.g. "XL", "Red"
        public decimal? PriceModifier { get; set; } = 0;
        public int StockQuantity { get; set; } = 0;
        public string? SKU { get; set; }

        // Foreign Key
        public Guid ProductId { get; set; }

        // Navigation
        public Product Product { get; set; } = null!;
    }
}