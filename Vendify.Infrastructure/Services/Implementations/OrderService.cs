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
        private readonly IPaymentVerificationService _paymentVerifier;

        public OrderService(
            VendifyDbContext context,
            IPaymentVerificationService paymentVerifier)
        {
            _context = context;
            _paymentVerifier = paymentVerifier;
        }

        public async Task<ApiResponse<OrderDto>> CreateOrderAsync(
            CreateOrderRequest request, string storeSlug)
        {
            // Get store
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());

            if (store == null)
                return ApiResponse<OrderDto>.FailureResponse("Store not found");

            // Parse and validate IDs up front
            var lines = new List<(Guid ProductId, Guid? VariantId, int Quantity)>();
            foreach (var item in request.Items)
            {
                if (!Guid.TryParse(item.ProductId, out var productGuid))
                    return ApiResponse<OrderDto>.FailureResponse(
                        $"Invalid product ID: {item.ProductId}");

                Guid? variantGuid = null;
                if (!string.IsNullOrWhiteSpace(item.VariantId))
                {
                    if (!Guid.TryParse(item.VariantId, out var parsedVariant))
                        return ApiResponse<OrderDto>.FailureResponse(
                            $"Invalid variant ID: {item.VariantId}");
                    variantGuid = parsedVariant;
                }

                if (item.Quantity < 1)
                    return ApiResponse<OrderDto>.FailureResponse("Quantity must be at least 1");

                lines.Add((productGuid, variantGuid, item.Quantity));
            }

            // Validate items and calculate totals
            decimal subtotal = 0;
            var orderItems = new List<OrderItem>();
            var products = new Dictionary<Guid, Product>();

            foreach (var line in lines)
            {
                var product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Variants)
                    .FirstOrDefaultAsync(p =>
                        p.Id == line.ProductId &&
                        p.StoreId == store.Id &&
                        p.IsPublished);

                if (product == null)
                    return ApiResponse<OrderDto>.FailureResponse(
                        $"Product {line.ProductId} not found or unavailable");

                if (product.TrackInventory && product.StockQuantity < line.Quantity)
                    return ApiResponse<OrderDto>.FailureResponse(
                        $"Insufficient stock for {product.Name}");

                products[product.Id] = product;

                decimal unitPrice = product.Price;
                string? variantInfo = null;

                if (line.VariantId.HasValue)
                {
                    var variant = product.Variants
                        .FirstOrDefault(v => v.Id == line.VariantId.Value);

                    if (variant != null)
                    {
                        unitPrice += variant.PriceModifier ?? 0;
                        variantInfo = $"{variant.Name}: {variant.Value}";
                    }
                }

                var itemTotal = unitPrice * line.Quantity;
                subtotal += itemTotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    VariantInfo = variantInfo,
                    Quantity = line.Quantity,
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

            // Calculate shipping fee (same rule as the cart)
            decimal shippingFee = 0;
            var storeZones = await _context.ShippingZones
                .Where(sz => sz.StoreId == store.Id && sz.IsActive)
                .ToListAsync();

                        if (storeZones.Any())
            {
                // The customer picked a delivery option: use exactly that one.
                // Otherwise fall back to the automatic rule.
                var shippingZone = request.ShippingZoneId.HasValue
                    ? ShippingZoneMatcher.FindById(
                        storeZones,
                        request.ShippingZoneId.Value,
                        request.ShippingAddress.State,
                        request.ShippingAddress.Country)
                    : ShippingZoneMatcher.FindZone(
                        storeZones,
                        request.ShippingAddress.State,
                        request.ShippingAddress.Country,
                        request.ShippingAddress.City);

                if (shippingZone == null)
                    return ApiResponse<OrderDto>.FailureResponse(
                        $"Sorry, this store does not deliver to {request.ShippingAddress.State} yet.");

                shippingFee = ShippingZoneMatcher.FeeOf(shippingZone);
            }

            var total = subtotal - discount + shippingFee;


            // Verify the payment with the payment provider
            var isPaid = false;
            if (!string.IsNullOrWhiteSpace(request.PaymentReference))
            {
                var reference = request.PaymentReference.Trim();

                var alreadyUsed = await _context.Orders
                    .AnyAsync(o => o.PaymentReference == reference);
                if (alreadyUsed)
                    return ApiResponse<OrderDto>.FailureResponse(
                        "This payment has already been used for another order.");

                var check = await _paymentVerifier.VerifyAsync(
                    request.PaymentMethod, reference, total);
                if (!check.IsVerified)
                    return ApiResponse<OrderDto>.FailureResponse(check.Message);

                isPaid = true;
            }

            var paymentStatus = isPaid
                ? PaymentStatus.Paid
                : PaymentStatus.Unpaid;

            // Create order
            var order = new Order
            {
                OrderNumber = await GenerateOrderNumberAsync(),
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Status = isPaid
                    ? OrderStatus.Confirmed
                    : OrderStatus.Pending,
                PaymentStatus = paymentStatus,
                PaymentMethod = request.PaymentMethod,
                PaymentReference = isPaid
                    ? request.PaymentReference!.Trim()
                    : null,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Discount = discount,
                Total = total,
                CouponCode = request.CouponCode?.ToUpper(),
                Notes = request.Notes,
                StoreId = store.Id,
                Items = orderItems,
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

            // in the Order initializer, add:  Items = orderItems,
            _context.Orders.Add(order);

            // Deduct stock using the products already loaded above
            foreach (var line in lines)
            {
                var product = products[line.ProductId];
                if (product.TrackInventory)
                    product.StockQuantity -= line.Quantity;
            }

            await _context.SaveChangesAsync();   // single save: order + items + stock

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