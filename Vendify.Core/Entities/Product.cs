using Vendify.Core.Enums;

namespace Vendify.Core.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public int StockQuantity { get; set; } = 0;
        public bool TrackInventory { get; set; } = true;
        public bool IsDigital { get; set; } = false;
        public string? DigitalFileUrl { get; set; }
        public string? DigitalFileKey { get; set; }
        public ProductType Type { get; set; } = ProductType.Physical;
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; } = "kg";
        public bool IsPublished { get; set; } = false;
        public int SalesCount { get; set; } = 0;
        public string? Tags { get; set; }

        // Foreign Keys
        public Guid StoreId { get; set; }
        public Guid? CategoryId { get; set; }

        // Navigation
        public Store Store { get; set; } = null!;
        public Category? Category { get; set; }
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}