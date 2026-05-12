using System.ComponentModel.DataAnnotations;
using Vendify.Core.Enums;

namespace Vendify.Application.DTOs.Coupon
{
    // ─── REQUEST DTOs ────────────────────────────────────────

    public class CreateCouponRequest
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public DiscountType DiscountType { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal DiscountValue { get; set; }

        public decimal? MinimumOrderAmount { get; set; }
        public int? UsageLimit { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateCouponRequest
    {
        public decimal? DiscountValue { get; set; }
        public decimal? MinimumOrderAmount { get; set; }
        public int? UsageLimit { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ValidateCouponRequest
    {
        [Required]
        public string Code { get; set; } = string.Empty;
        [Required]
        public string StoreSlug { get; set; } = string.Empty;
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal OrderAmount { get; set; }
    }

    // ─── RESPONSE DTOs ───────────────────────────────────────

    public class CouponDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal? MinimumOrderAmount { get; set; }
        public int? UsageLimit { get; set; }
        public int UsageCount { get; set; }
        public int? RemainingUses { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsExpired { get; set; }
        public Guid StoreId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CouponValidateDto
    {
        public bool IsValid { get; set; }
        public string? Message { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal FinalAmount { get; set; }
    }
}