using Microsoft.EntityFrameworkCore;
using Vendify.Core.Entities;

namespace Vendify.Infrastructure.Data
{
    public class VendifyDbContext : DbContext
    {
        public VendifyDbContext(DbContextOptions<VendifyDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Store> Stores { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<ShippingZone> ShippingZones { get; set; }
        public DbSet<Coupon> Coupons { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(VendifyDbContext).Assembly);

            // Global soft delete filters
            modelBuilder.Entity<Product>()
                .HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Order>()
                .HasQueryFilter(o => !o.IsDeleted);
            modelBuilder.Entity<Store>()
                .HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<User>()
                .HasQueryFilter(u => !u.IsDeleted);
            // Add these alongside your existing filters
            modelBuilder.Entity<Category>()
                .HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Coupon>()
                .HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<OrderItem>()
                .HasQueryFilter(oi => !oi.IsDeleted);
            modelBuilder.Entity<ProductImage>()
                .HasQueryFilter(pi => !pi.IsDeleted);
            modelBuilder.Entity<ProductVariant>()
                .HasQueryFilter(pv => !pv.IsDeleted);
            modelBuilder.Entity<ShippingZone>()
                .HasQueryFilter(sz => !sz.IsDeleted);

            // ShippingAddress as owned entity
            modelBuilder.Entity<Order>()
                .OwnsOne(o => o.ShippingAddress, sa =>
                {
                    sa.Property(a => a.FullName).HasMaxLength(200);
                    sa.Property(a => a.AddressLine1).HasMaxLength(500);
                    sa.Property(a => a.City).HasMaxLength(100);
                    sa.Property(a => a.State).HasMaxLength(100);
                    sa.Property(a => a.Country).HasMaxLength(100);
                });

            // Unique indexes
            modelBuilder.Entity<Store>()
                .HasIndex(s => s.Slug).IsUnique();
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Coupon>()
                .HasIndex(c => new { c.StoreId, c.Code }).IsUnique();

            // Decimal precision
            modelBuilder.Entity<Product>()
                .Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Product>()
                .Property(p => p.CompareAtPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Order>()
                .Property(o => o.Total).HasPrecision(18, 2);
            modelBuilder.Entity<Order>()
                .Property(o => o.Subtotal).HasPrecision(18, 2);
            modelBuilder.Entity<Order>()
                .Property(o => o.ShippingFee).HasPrecision(18, 2);
            modelBuilder.Entity<Order>()
                .Property(o => o.Discount).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.TotalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<ShippingZone>()
                .Property(sz => sz.Fee).HasPrecision(18, 2);
            modelBuilder.Entity<Coupon>()
                .Property(c => c.DiscountValue).HasPrecision(18, 2);
            modelBuilder.Entity<Coupon>()
                .Property(c => c.MinimumOrderAmount).HasPrecision(18, 2);
        }
    }
}