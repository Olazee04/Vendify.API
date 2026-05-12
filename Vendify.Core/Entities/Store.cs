using Vendify.Core.Enums;

namespace Vendify.Core.Entities
{
    public class Store : BaseEntity
    {
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
        public StoreStatus Status { get; set; } = StoreStatus.Active;

        // Foreign Key
        public Guid UserId { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<ShippingZone> ShippingZones { get; set; } = new List<ShippingZone>();
        public ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();
    }
}