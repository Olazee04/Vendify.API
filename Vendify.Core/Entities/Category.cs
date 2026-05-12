namespace Vendify.Core.Entities
{
    public class Category : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; } = 0;

        // Foreign Key
        public Guid StoreId { get; set; }

        // Navigation
        public Store Store { get; set; } = null!;
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}