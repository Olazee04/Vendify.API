using System.ComponentModel.DataAnnotations;
using Vendify.Core.Enums;

namespace Vendify.Application.DTOs.Store
{
    // ─── REQUEST DTOs ────────────────────────────────────────

    public class CreateStoreRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [RegularExpression(@"^[a-z0-9-]+$",
            ErrorMessage = "Slug can only contain lowercase letters, numbers and hyphens")]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // All social media optional individually
        // but at least ONE must be provided (validated in service)
        public string? WhatsAppNumber { get; set; }
        public string? InstagramHandle { get; set; }
        public string? FacebookPageUrl { get; set; }
        public string? TwitterHandle { get; set; }

        public string? SupportEmail { get; set; }
        public string Currency { get; set; } = "NGN";
    }

    public class UpdateStoreRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? WhatsAppNumber { get; set; }
        public string? InstagramHandle { get; set; }
        public string? FacebookPageUrl { get; set; }
        public string? TwitterHandle { get; set; }
        public string? SupportEmail { get; set; }
        public string? Currency { get; set; }
        public string? CustomDomain { get; set; }
        public string? ThemeId { get; set; }
    }

    public class UpdateStoreSlugRequest
    {
        [Required]
        [MaxLength(100)]
        [RegularExpression(@"^[a-z0-9-]+$",
            ErrorMessage = "Slug can only contain lowercase letters, numbers and hyphens")]
        public string Slug { get; set; } = string.Empty;
    }

    // ─── RESPONSE DTOs ───────────────────────────────────────

    public class StoreDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? ThemeId { get; set; }
        public string? CustomDomain { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? InstagramHandle { get; set; }
        public string? FacebookPageUrl { get; set; }
        public string? TwitterHandle { get; set; }
        public string? SupportEmail { get; set; }
        public string Currency { get; set; } = "NGN";
        public string Status { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public StoreStatsDto Stats { get; set; } = new();
    }

    public class StoreStatsDto
    {
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class StorePublicDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? ThemeId { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? InstagramHandle { get; set; }
        public string? FacebookPageUrl { get; set; }
        public string Currency { get; set; } = "NGN";
    }

    public class SlugCheckDto
    {
        public bool IsAvailable { get; set; }
        public string Slug { get; set; } = string.Empty;
    }
}