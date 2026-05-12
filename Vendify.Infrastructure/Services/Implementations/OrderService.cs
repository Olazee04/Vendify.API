using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Order;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly VendifyDbContext _context;

        public OrderService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<OrderDto>> CreateOrderAsync(
            CreateOrderRequest request, string storeSlug)
        {
            // Get store
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());

            if (store == null)
                return ApiResponse<OrderDto>.FailureResponse("Store not found");

            // Validate items and calculate totals
            decimal subtotal = 0;
            var orderItems = new List<OrderItem>();

            foreach (var item in request.Items)
            {
                var product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Variants)
                    .FirstOrDefaultAsync(p =>
                        p.Id == item.ProductId &&
                        p.StoreId == store.Id &&
                        p.IsPublished);

                if (product == null)
                    return ApiResponse<OrderDto>.FailureResponse(
                        $"Product {item.ProductId} not found or unavailable");

                // Check stock
                if (product.TrackInventory &&
                    product.StockQuantity < item.Quantity)
                    return ApiResponse<OrderDto>.FailureResponse(
                        $"Insufficient stock for {product.Name}");

                decimal unitPrice = product.Price;
                string? variantInfo = null;

                // Apply variant price modifier
                if (item.VariantId.HasValue)
                {
                    var variant = product.Variants
                        .FirstOrDefault(v => v.Id == item.VariantId);

                    if (variant != null)
                    {
                        unitPrice += variant.PriceModifier ?? 0;
                        variantInfo = $"{variant.Name}: {variant.Value}";
                    }
                }

                var itemTotal = unitPrice * item.Quantity;
                subtotal += itemTotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    VariantInfo = variantInfo,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = itemTotal,
                    ProductImageUrl = product.Images
                        .FirstOrDefault(i => i.IsPrimary)?.Url
                        ?? product.Images.FirstOrDefault()?.Url
                });
            }

            // Apply coupon if provided
            decimal discount = 0;
            if (!string.IsNullOrEmpty(request.CouponCode))
            {
                var coupon = await _context.Coupons
                    .FirstOrDefaultAsync(c =>
                        c.Code == request.CouponCode.ToUpper() &&
                        c.StoreId == store.Id &&
                        c.IsActive &&
                        (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow) &&
                        (c.UsageLimit == null || c.UsageCount < c.UsageLimit));

                if (coupon != null &&
                    (coupon.MinimumOrderAmount == null ||
                     subtotal >= coupon.MinimumOrderAmount))
                {
                    discount = coupon.DiscountType == DiscountType.Percentage
                        ? subtotal * (coupon.DiscountValue / 100)
                        : coupon.DiscountValue;

                    discount = Math.Min(discount, subtotal);
                    coupon.UsageCount++;
                }
            }

            // Calculate shipping fee
            decimal shippingFee = 0;
            var shippingZone = await _context.ShippingZones
                .FirstOrDefaultAsync(sz =>
                    sz.StoreId == store.Id &&
                    sz.IsActive &&
                    (sz.Name.ToLower().Contains(
                        request.ShippingAddress.State.ToLower()) ||
                     sz.Name.ToLower() == "nationwide" ||
                     sz.Name.ToLower() == "all"));

            if (shippingZone != null)
                shippingFee = shippingZone.Fee;

            var total = subtotal - discount + shippingFee;

            // Create order
            var order = new Order
            {
                OrderNumber = await GenerateOrderNumberAsync(),
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Unpaid,
                PaymentMethod = request.PaymentMethod,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Discount = discount,
                Total = total,
                CouponCode = request.CouponCode?.ToUpper(),
                Notes = request.Notes,
                StoreId = store.Id,
                ShippingAddress = new Core.Entities.ShippingAddress
                {
                    FullName = request.ShippingAddress.FullName,
                    PhoneNumber = request.ShippingAddress.PhoneNumber,
                    AddressLine1 = request.ShippingAddress.AddressLine1,
                    AddressLine2 = request.ShippingAddress.AddressLine2,
                    City = request.ShippingAddress.City,
                    State = request.ShippingAddress.State,
                    Country = request.ShippingAddress.Country,
                    PostalCode = request.ShippingAddress.PostalCode
                }
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Add order items
            orderItems.ForEach(i => i.OrderId = order.Id);
            _context.OrderItems.AddRange(orderItems);

            // Deduct stock
            foreach (var item in request.Items)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                if (product?.TrackInventory == true)
                    product.StockQuantity -= item.Quantity;
            }

            await _context.SaveChangesAsync();

            var created = await GetFullOrderAsync(order.Id);
            return ApiResponse<OrderDto>.SuccessResponse(
                created!, "Order created successfully");
        }

        public async Task<ApiResponse<OrderDto>> GetOrderByIdAsync(
            Guid orderId, Guid storeId)
        {
            var order = await GetFullOrderAsync(orderId);

            if (order == null)
                return ApiResponse<OrderDto>.FailureResponse("Order not found");

            return ApiResponse<OrderDto>.SuccessResponse(order);
        }

        public async Task<ApiResponse<OrderDto>> GetOrderByNumberAsync(
            string orderNumber)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null)
                return ApiResponse<OrderDto>.FailureResponse("Order not found");

            return ApiResponse<OrderDto>.SuccessResponse(MapToOrderDto(order));
        }

        public async Task<ApiResponse<PagedOrdersDto>> GetStoreOrdersAsync(
            Guid storeId, OrderFilterRequest filter)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                .Where(o => o.StoreId == storeId);

            if (filter.Status.HasValue)
                query = query.Where(o => o.Status == filter.Status);

            if (filter.PaymentStatus.HasValue)
                query = query.Where(o =>
                    o.PaymentStatus == filter.PaymentStatus);

            if (filter.FromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.FromDate);

            if (filter.ToDate.HasValue)
                query = query.Where(o => o.CreatedAt <= filter.ToDate);

            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(o =>
                    o.OrderNumber.Contains(filter.Search) ||
                    o.CustomerName.ToLower()
                        .Contains(filter.Search.ToLower()) ||
                    o.CustomerEmail.ToLower()
                        .Contains(filter.Search.ToLower()));

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(
                (double)totalCount / filter.PageSize);

            return ApiResponse<PagedOrdersDto>.SuccessResponse(
                new PagedOrdersDto
                {
                    Orders = orders.Select(MapToOrderDto).ToList(),
                    TotalCount = totalCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalPages = totalPages,
                    HasNextPage = filter.Page < totalPages,
                    HasPreviousPage = filter.Page > 1
                });
        }

        public async Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(
            Guid orderId, UpdateOrderStatusRequest request, Guid storeId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && o.StoreId == storeId);

            if (order == null)
                return ApiResponse<OrderDto>.FailureResponse("Order not found");

            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            if (request.TrackingNumber != null)
                order.TrackingNumber = request.TrackingNumber;

            if (request.Status == OrderStatus.Shipped)
                order.ShippedAt = DateTime.UtcNow;

            if (request.Status == OrderStatus.Delivered)
                order.DeliveredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ApiResponse<OrderDto>.SuccessResponse(
                MapToOrderDto(order), "Order status updated");
        }

        public async Task<ApiResponse<OrderDto>> UpdatePaymentStatusAsync(
            Guid orderId, string paymentReference, Guid storeId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && o.StoreId == storeId);

            if (order == null)
                return ApiResponse<OrderDto>.FailureResponse("Order not found");

            order.PaymentStatus = PaymentStatus.Paid;
            order.PaymentReference = paymentReference;
            order.Status = OrderStatus.Confirmed;
            order.UpdatedAt = DateTime.UtcNow;

            // Update sales count
            foreach (var item in order.Items)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product != null)
                    product.SalesCount += item.Quantity;
            }

            await _context.SaveChangesAsync();

            return ApiResponse<OrderDto>.SuccessResponse(
                MapToOrderDto(order), "Payment confirmed");
        }

        public async Task<ApiResponse> CancelOrderAsync(
            Guid orderId, Guid storeId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && o.StoreId == storeId);

            if (order == null)
                return ApiResponse.FailureResponse("Order not found");

            if (order.Status == OrderStatus.Shipped ||
                order.Status == OrderStatus.Delivered)
                return ApiResponse.FailureResponse(
                    "Cannot cancel a shipped or delivered order");

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;

            // Restore stock
            foreach (var item in order.Items)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product?.TrackInventory == true)
                    product.StockQuantity += item.Quantity;
            }

            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse("Order cancelled successfully");
        }

        // ── Private Helpers ──────────────────────────────────

        private async Task<string> GenerateOrderNumberAsync()
        {
            var today = DateTime.UtcNow;
            var prefix = $"VND-{today:yyyyMMdd}";
            var count = await _context.Orders
                .CountAsync(o => o.OrderNumber.StartsWith(prefix));
            return $"{prefix}-{(count + 1):D4}";
        }

        private async Task<OrderDto?> GetFullOrderAsync(Guid orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            return order == null ? null : MapToOrderDto(order);
        }

        private static OrderDto MapToOrderDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                CustomerPhone = order.CustomerPhone,
                Status = order.Status.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                PaymentMethod = order.PaymentMethod.ToString(),
                PaymentReference = order.PaymentReference,
                Subtotal = order.Subtotal,
                ShippingFee = order.ShippingFee,
                Discount = order.Discount,
                Total = order.Total,
                CouponCode = order.CouponCode,
                Notes = order.Notes,
                TrackingNumber = order.TrackingNumber,
                ShippedAt = order.ShippedAt,
                DeliveredAt = order.DeliveredAt,
                ShippingAddress = new ShippingAddressDto
                {
                    FullName = order.ShippingAddress.FullName,
                    PhoneNumber = order.ShippingAddress.PhoneNumber,
                    AddressLine1 = order.ShippingAddress.AddressLine1,
                    AddressLine2 = order.ShippingAddress.AddressLine2,
                    City = order.ShippingAddress.City,
                    State = order.ShippingAddress.State,
                    Country = order.ShippingAddress.Country,
                    PostalCode = order.ShippingAddress.PostalCode
                },
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    VariantInfo = i.VariantInfo,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    ProductImageUrl = i.ProductImageUrl
                }).ToList(),
                CreatedAt = order.CreatedAt
            };
        }
    }
}