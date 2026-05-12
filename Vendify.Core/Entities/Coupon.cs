using Vendify.Core.Enums;

namespace Vendify.Core.Entities
{
    public class Coupon : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MinimumOrderAmount { get; set; }
        public int? UsageLimit { get; set; }
        public int UsageCount { get; set; } = 0;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Foreign Key
        public Guid StoreId { get; set; }

        // Navigation
        public Store Store { get; set; } = null!;
    }
}