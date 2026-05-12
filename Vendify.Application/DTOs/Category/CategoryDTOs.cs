using System.ComponentModel.DataAnnotations;

namespace Vendify.Application.DTOs.Category
{
    // ─── REQUEST DTOs ────────────────────────────────────────

    public class CreateCategoryRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; } = 0;
    }

    public class UpdateCategoryRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int? SortOrder { get; set; }
    }

    // ─── RESPONSE DTOs ───────────────────────────────────────

    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public int ProductCount { get; set; }
        public Guid StoreId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}