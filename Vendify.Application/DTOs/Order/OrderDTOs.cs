using System.ComponentModel.DataAnnotations;
using Vendify.Core.Enums;

namespace Vendify.Application.DTOs.Order
{
    // ─── REQUEST DTOs ────────────────────────────────────────

    public class CreateOrderRequest
    {
        [Required]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required]
        public ShippingAddressRequest ShippingAddress { get; set; } = new();

        [Required]
        public List<OrderItemRequest> Items { get; set; } = new();

        public string? CouponCode { get; set; }
        public string? Notes { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
    }

    public class ShippingAddressRequest
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required]
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        [Required]
        public string City { get; set; } = string.Empty;
        [Required]
        public string State { get; set; } = string.Empty;
        [Required]
        public string Country { get; set; } = "Nigeria";
        public string? PostalCode { get; set; }
    }

    public class OrderItemRequest
    {
        [Required]
        public Guid ProductId { get; set; }
        public Guid? VariantId { get; set; }
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
    }

    public class UpdateOrderStatusRequest
    {
        [Required]
        public OrderStatus Status { get; set; }
        public string? TrackingNumber { get; set; }
        public string? Note { get; set; }
    }

    public class OrderFilterRequest
    {
        public OrderStatus? Status { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ─── RESPONSE DTOs ───────────────────────────────────────

    public class OrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? CouponCode { get; set; }
        public string? Notes { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public ShippingAddressDto ShippingAddress { get; set; } = new();
        public List<OrderItemDto> Items { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ShippingAddressDto
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
    }

    public class OrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? VariantInfo { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? ProductImageUrl { get; set; }
    }

    public class PagedOrdersDto
    {
        public List<OrderDto> Orders { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}