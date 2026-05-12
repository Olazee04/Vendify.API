using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vendify.Application.DTOs.Payment;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WebhooksController : ControllerBase
    {
        private readonly VendifyDbContext _context;
        private readonly IConfiguration _configuration;

        public WebhooksController(
            VendifyDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // Paystack Webhook
        [HttpPost("paystack")]
        public async Task<IActionResult> PaystackWebhook()
        {
            var secretKey = _configuration["Paystack:SecretKey"]!;

            // Read raw body
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // Verify signature
            var signature = Request.Headers["x-paystack-signature"]
                .ToString();
            var hash = ComputeHmacSha512(secretKey, body);

            if (hash != signature)
                return Unauthorized();

            var webhook = JsonSerializer.Deserialize<PaystackWebhookDto>(
                body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (webhook?.Event == "charge.success" &&
                webhook.Data?.Status == "success")
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o =>
                        o.PaymentReference == webhook.Data.Reference);

                if (order != null &&
                    order.PaymentStatus != PaymentStatus.Paid)
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

            return Ok();
        }

        // Flutterwave Webhook
        [HttpPost("flutterwave")]
        public async Task<IActionResult> FlutterwaveWebhook()
        {
            var secretHash = _configuration["Flutterwave:WebhookHash"];
            var signature = Request.Headers["verif-hash"].ToString();

            if (signature != secretHash)
                return Unauthorized();

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            var webhook = JsonSerializer.Deserialize<FlutterwaveWebhookDto>(
                body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (webhook?.Event == "charge.completed" &&
                webhook.Data?.Status == "successful")
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o =>
                        o.PaymentReference == webhook.Data.TxRef);

                if (order != null &&
                    order.PaymentStatus != PaymentStatus.Paid)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.Status = OrderStatus.Confirmed;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }

        private static string ComputeHmacSha512(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return Convert.ToHexString(hash).ToLower();
        }
    }
}