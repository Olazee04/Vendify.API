using System.ComponentModel.DataAnnotations;
using Vendify.Core.Enums;

namespace Vendify.Application.DTOs.Shipping
{
    // ─── REQUEST DTOs ────────────────────────────────────────

    public class CreateShippingZoneRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        // e.g. "Lagos", "Abuja", "Nationwide", "International"

        public string? Description { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Fee { get; set; }

        public ShippingType Type { get; set; } = ShippingType.FlatRate;
        public bool IsActive { get; set; } = true;
        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }
    }

    public class UpdateShippingZoneRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Fee { get; set; }
        public ShippingType? Type { get; set; }
        public bool? IsActive { get; set; }
        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }
    }

    public class CalculateShippingRequest
    {
        [Required]
        public string StoreSlug { get; set; } = string.Empty;
        [Required]
        public string State { get; set; } = string.Empty;
        public string? Country { get; set; } = "Nigeria";
    }

    // ─── RESPONSE DTOs ───────────────────────────────────────

    public class ShippingZoneDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Fee { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }
        public string? DeliveryEstimate { get; set; }
        public Guid StoreId { get; set; }
    }

    public class ShippingCalculateDto
    {
        public decimal Fee { get; set; }
        public string ZoneName { get; set; } = string.Empty;
        public string? DeliveryEstimate { get; set; }
        public bool IsFreeShipping { get; set; }
    }
}