namespace Vendify.Core.Entities
{
    public class ProductImage : BaseEntity
    {
        public string Url { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsPrimary { get; set; } = false;
        public int SortOrder { get; set; } = 0;

        // Foreign Key
        public Guid ProductId { get; set; }

        // Navigation
        public Product Product { get; set; } = null!;
    }
}