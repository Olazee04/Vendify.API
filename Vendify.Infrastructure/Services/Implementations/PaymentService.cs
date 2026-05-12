using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Payment;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly VendifyDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public PaymentService(
            VendifyDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        // ── UNIFIED METHODS ──────────────────────────────────

        public async Task<ApiResponse<PaymentInitiateDto>> InitiatePaymentAsync(
            InitiatePaymentRequest request, Guid storeId)
        {
            return request.Provider.ToLower() switch
            {
                "paystack" => await InitiatePaystackPaymentAsync(
                    request.OrderId, storeId, request.CallbackUrl),
                "flutterwave" => await InitiateFlutterwavePaymentAsync(
                    request.OrderId, storeId, request.CallbackUrl),
                _ => ApiResponse<PaymentInitiateDto>.FailureResponse(
                    "Invalid payment provider. Use: paystack, flutterwave")
            };
        }

        public async Task<ApiResponse<PaymentVerifyDto>> VerifyPaymentAsync(
            VerifyPaymentRequest request, Guid storeId)
        {
            return request.Provider.ToLower() switch
            {
                "paystack" => await VerifyPaystackPaymentAsync(
                    request.Reference, storeId),
                "flutterwave" => await VerifyFlutterwavePaymentAsync(
                    request.Reference, storeId),
                "stripe" => await VerifyStripePaymentAsync(
                    request.Reference, storeId),
                _ => ApiResponse<PaymentVerifyDto>.FailureResponse(
                    "Invalid payment provider")
            };
        }

        // ── PAYSTACK ─────────────────────────────────────────

        public async Task<ApiResponse<PaymentInitiateDto>>
            InitiatePaystackPaymentAsync(
            Guid orderId, Guid storeId, string? callbackUrl)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && o.StoreId == storeId);

            if (order == null)
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    "Order not found");

            if (order.PaymentStatus == PaymentStatus.Paid)
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    "Order is already paid");

            var secretKey = _configuration["Paystack:SecretKey"];
            var baseUrl = _configuration["Paystack:BaseUrl"];

            // Paystack amount is in kobo (multiply by 100)
            var amountInKobo = (long)(order.Total * 100);

            var reference = $"VND-PSK-{order.OrderNumber}-{Guid.NewGuid():N}"
                .Substring(0, 40);

            var payload = new
            {
                email = order.CustomerEmail,
                amount = amountInKobo,
                reference = reference,
                callback_url = callbackUrl
                    ?? _configuration["Paystack:CallbackUrl"]
                    ?? "https://vendify.com/payment/callback",
                metadata = new
                {
                    order_id = order.Id.ToString(),
                    order_number = order.OrderNumber,
                    customer_name = order.CustomerName
                }
            };

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");

            var response = await _httpClient
                .PostAsync($"{baseUrl}/transaction/initialize", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    "Failed to initialize Paystack payment");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.GetProperty("status").GetBoolean())
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    root.GetProperty("message").GetString()
                    ?? "Paystack initialization failed");

            var data = root.GetProperty("data");
            var authorizationUrl = data
                .GetProperty("authorization_url").GetString()!;

            // Save reference to order
            order.PaymentReference = reference;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<PaymentInitiateDto>.SuccessResponse(
                new PaymentInitiateDto
                {
                    Provider = "Paystack",
                    PaymentUrl = authorizationUrl,
                    Reference = reference,
                    Amount = order.Total,
                    Currency = "NGN",
                    OrderId = order.Id
                }, "Payment initialized successfully");
        }

        public async Task<ApiResponse<PaymentVerifyDto>>
            VerifyPaystackPaymentAsync(string reference, Guid storeId)
        {
            var secretKey = _configuration["Paystack:SecretKey"];
            var baseUrl = _configuration["Paystack:BaseUrl"];

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);

            var response = await _httpClient
                .GetAsync($"{baseUrl}/transaction/verify/{reference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ApiResponse<PaymentVerifyDto>.FailureResponse(
                    "Failed to verify payment");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var data = root.GetProperty("data");
            var status = data.GetProperty("status").GetString();
            var amount = data.GetProperty("amount").GetInt64() / 100m;

            var isPaid = status == "success";

            if (isPaid)
            {
                // Find and update order
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o =>
                        o.PaymentReference == reference &&
                        o.StoreId == storeId);

                if (order != null)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.Status = OrderStatus.Confirmed;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Update sales count
                    var items = await _context.OrderItems
                        .Where(i => i.OrderId == order.Id)
                        .ToListAsync();

                    foreach (var item in items)
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                        if (product != null)
                            product.SalesCount += item.Quantity;
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return ApiResponse<PaymentVerifyDto>.SuccessResponse(
                new PaymentVerifyDto
                {
                    IsPaid = isPaid,
                    Reference = reference,
                    Provider = "Paystack",
                    Amount = amount,
                    Currency = "NGN",
                    Status = status ?? "unknown",
                    OrderId = Guid.Empty
                });
        }

        // ── FLUTTERWAVE ──────────────────────────────────────

        public async Task<ApiResponse<PaymentInitiateDto>>
            InitiateFlutterwavePaymentAsync(
            Guid orderId, Guid storeId, string? callbackUrl)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && o.StoreId == storeId);

            if (order == null)
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    "Order not found");

            if (order.PaymentStatus == PaymentStatus.Paid)
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    "Order is already paid");

            var secretKey = _configuration["Flutterwave:SecretKey"];
            var baseUrl = _configuration["Flutterwave:BaseUrl"];

            var txRef = $"VND-FLW-{order.OrderNumber}-{Guid.NewGuid():N}"
                .Substring(0, 40);

            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Id == storeId);

            var payload = new
            {
                tx_ref = txRef,
                amount = order.Total,
                currency = store?.Currency ?? "NGN",
                redirect_url = callbackUrl
                    ?? _configuration["Flutterwave:CallbackUrl"]
                    ?? "https://vendify.com/payment/callback",
                customer = new
                {
                    email = order.CustomerEmail,
                    name = order.CustomerName,
                    phonenumber = order.CustomerPhone
                },
                meta = new
                {
                    order_id = order.Id.ToString(),
                    order_number = order.OrderNumber
                },
                customizations = new
                {
                    title = store?.Name ?? "Vendify Store",
                    description = $"Payment for order {order.OrderNumber}"
                }
            };

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");

            var response = await _httpClient
                .PostAsync($"{baseUrl}/payments", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    "Failed to initialize Flutterwave payment");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.GetProperty("status").GetString() != "success")
                return ApiResponse<PaymentInitiateDto>.FailureResponse(
                    root.GetProperty("message").GetString()
                    ?? "Flutterwave initialization failed");

            var paymentLink = root
                .GetProperty("data")
                .GetProperty("link")
                .GetString()!;

            order.PaymentReference = txRef;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<PaymentInitiateDto>.SuccessResponse(
                new PaymentInitiateDto
                {
                    Provider = "Flutterwave",
                    PaymentUrl = paymentLink,
                    Reference = txRef,
                    Amount = order.Total,
                    Currency = store?.Currency ?? "NGN",
                    OrderId = order.Id
                }, "Payment initialized successfully");
        }

        public async Task<ApiResponse<PaymentVerifyDto>>
            VerifyFlutterwavePaymentAsync(string reference, Guid storeId)
        {
            var secretKey = _configuration["Flutterwave:SecretKey"];
            var baseUrl = _configuration["Flutterwave:BaseUrl"];

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);

            var response = await _httpClient
                .GetAsync($"{baseUrl}/transactions/verify_by_reference" +
                    $"?tx_ref={reference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ApiResponse<PaymentVerifyDto>.FailureResponse(
                    "Failed to verify payment");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var data = root.GetProperty("data");
            var status = data.GetProperty("status").GetString();
            var amount = data.GetProperty("amount").GetDecimal();
            var currency = data.GetProperty("currency").GetString() ?? "NGN";

            var isPaid = status == "successful";

            if (isPaid)
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o =>
                        o.PaymentReference == reference &&
                        o.StoreId == storeId);

                if (order != null)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.Status = OrderStatus.Confirmed;
                    order.UpdatedAt = DateTime.UtcNow;

                    var items = await _context.OrderItems
                        .Where(i => i.OrderId == order.Id)
                        .ToListAsync();

                    foreach (var item in items)
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                        if (product != null)
                            product.SalesCount += item.Quantity;
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return ApiResponse<PaymentVerifyDto>.SuccessResponse(
                new PaymentVerifyDto
                {
                    IsPaid = isPaid,
                    Reference = reference,
                    Provider = "Flutterwave",
                    Amount = amount,
                    Currency = currency,
                    Status = status ?? "unknown",
                    OrderId = Guid.Empty
                });
        }

        // ── STRIPE ───────────────────────────────────────────

        public async Task<ApiResponse<StripePaymentDto>>
            InitiateStripePaymentAsync(
            Guid orderId, Guid storeId, string currency)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o =>
                    o.Id == orderId && o.StoreId == storeId);

            if (order == null)
                return ApiResponse<StripePaymentDto>.FailureResponse(
                    "Order not found");

            if (order.PaymentStatus == PaymentStatus.Paid)
                return ApiResponse<StripePaymentDto>.FailureResponse(
                    "Order is already paid");

            StripeConfiguration.ApiKey =
                _configuration["Stripe:SecretKey"];

            // Amount in cents
            var amountInCents = (long)(order.Total * 100);

            var options = new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = currency.ToLower(),
                Description = $"Payment for order {order.OrderNumber}",
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", order.Id.ToString() },
                    { "order_number", order.OrderNumber },
                    { "store_id", storeId.ToString() }
                },
                ReceiptEmail = order.CustomerEmail
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            order.PaymentReference = paymentIntent.Id;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<StripePaymentDto>.SuccessResponse(
                new StripePaymentDto
                {
                    ClientSecret = paymentIntent.ClientSecret,
                    PaymentIntentId = paymentIntent.Id,
                    Amount = order.Total,
                    Currency = currency,
                    OrderId = order.Id
                }, "Stripe payment initialized");
        }

        public async Task<ApiResponse<PaymentVerifyDto>>
            VerifyStripePaymentAsync(string paymentIntentId, Guid storeId)
        {
            StripeConfiguration.ApiKey =
                _configuration["Stripe:SecretKey"];

            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            var isPaid = paymentIntent.Status == "succeeded";

            if (isPaid)
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o =>
                        o.PaymentReference == paymentIntentId &&
                        o.StoreId == storeId);

                if (order != null)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.Status = OrderStatus.Confirmed;
                    order.UpdatedAt = DateTime.UtcNow;

                    var items = await _context.OrderItems
                        .Where(i => i.OrderId == order.Id)
                        .ToListAsync();

                    foreach (var item in items)
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                        if (product != null)
                            product.SalesCount += item.Quantity;
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return ApiResponse<PaymentVerifyDto>.SuccessResponse(
                new PaymentVerifyDto
                {
                    IsPaid = isPaid,
                    Reference = paymentIntentId,
                    Provider = "Stripe",
                    Amount = paymentIntent.Amount / 100m,
                    Currency = paymentIntent.Currency,
                    Status = paymentIntent.Status,
                    OrderId = Guid.Empty
                });
        }
    }
}