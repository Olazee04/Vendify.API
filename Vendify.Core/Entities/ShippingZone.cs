using Vendify.Core.Enums;

namespace Vendify.Core.Entities
{
    public class ShippingZone : BaseEntity
    {
        public string Name { get; set; } = string.Empty;  // e.g. "Lagos", "Abuja", "International"
        public string? Description { get; set; }
        public decimal Fee { get; set; }
        public ShippingType Type { get; set; } = ShippingType.FlatRate;
        public bool IsActive { get; set; } = true;
        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }

        // Foreign Key
        public Guid StoreId { get; set; }

        // Navigation
        public Store Store { get; set; } = null!;
    }
}