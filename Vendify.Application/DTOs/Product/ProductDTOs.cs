using System.ComponentModel.DataAnnotations;
using Vendify.Core.Enums;

namespace Vendify.Application.DTOs.Product
{
    // ─── REQUEST DTOs ────────────────────────────────────────

    public class CreateProductRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // Everything below is optional
        public string? Description { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        public decimal? CompareAtPrice { get; set; }
        public int StockQuantity { get; set; } = 0;
        public bool TrackInventory { get; set; } = true;
        public bool IsDigital { get; set; } = false;
        public string? DigitalFileUrl { get; set; }

        // Type defaults to Physical — user doesn't need to set it
        public ProductType Type { get; set; } = ProductType.Physical;

        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; } = "kg";

        // Defaults to false — merchant publishes manually later
        public bool IsPublished { get; set; } = false;

        public string? Tags { get; set; }
        public Guid? CategoryId { get; set; }

        // Variants are completely optional
        public List<CreateVariantRequest>? Variants { get; set; }
    }

    public class UpdateProductRequest
    {
        [MaxLength(200)]
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public int? StockQuantity { get; set; }
        public bool? TrackInventory { get; set; }
        public bool? IsPublished { get; set; }
        public string? SKU { get; set; }
        public string? Tags { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public Guid? CategoryId { get; set; }
    }

    public class CreateVariantRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Value { get; set; } = string.Empty;
        public decimal? PriceModifier { get; set; } = 0;
        public int StockQuantity { get; set; } = 0;
        public string? SKU { get; set; }
    }

    public class UpdateStockRequest
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
        public string? Note { get; set; }
    }

    public class ProductFilterRequest
    {
        public string? Search { get; set; }
        public Guid? CategoryId { get; set; }
        public ProductType? Type { get; set; }
        public bool? IsPublished { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "createdAt";
        public string SortOrder { get; set; } = "desc";
    }

    // ─── RESPONSE DTOs ───────────────────────────────────────

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public int StockQuantity { get; set; }
        public bool TrackInventory { get; set; }
        public bool IsDigital { get; set; }
        public string? DigitalFileUrl { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public bool IsPublished { get; set; }
        public string? Tags { get; set; }
        public int SalesCount { get; set; }
        public Guid StoreId { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public List<ProductImageDto> Images { get; set; } = new();
        public List<ProductVariantDto> Variants { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }
    }

    public class ProductVariantDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public decimal? PriceModifier { get; set; }
        public int StockQuantity { get; set; }
        public string? SKU { get; set; }
    }

    public class PagedProductsDto
    {
        public List<ProductDto> Products { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}