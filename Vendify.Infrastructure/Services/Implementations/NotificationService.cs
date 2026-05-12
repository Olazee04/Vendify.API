using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vendify.Application.DTOs.Notification;
using Vendify.Application.Services.Interfaces;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public NotificationService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        // ── EMAIL ────────────────────────────────────────────

        public async Task<bool> SendEmailAsync(SendEmailRequest request)
        {
            try
            {
                var emailConfig = _configuration.GetSection("Email");
                var host = emailConfig["Host"]!;
                var port = int.Parse(emailConfig["Port"] ?? "587");
                var username = emailConfig["Username"]!;
                var password = emailConfig["Password"]!;
                var fromName = emailConfig["FromName"] ?? "Vendify";
                var fromEmail = emailConfig["FromEmail"] ?? username;

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(
                    new MailboxAddress(request.ToName, request.ToEmail));
                message.Subject = request.Subject;

                var bodyBuilder = new BodyBuilder();
                if (request.IsHtml)
                    bodyBuilder.HtmlBody = request.Body;
                else
                    bodyBuilder.TextBody = request.Body;

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(
                    host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendOrderConfirmationAsync(OrderEmailDto order)
        {
            var itemsHtml = string.Join("", order.Items.Select(i => $@"
                <tr>
                    <td style='padding:8px;border-bottom:1px solid #eee'>
                        {i.ProductName}
                        {(i.VariantInfo != null ? $"<br><small style='color:#888'>{i.VariantInfo}</small>" : "")}
                    </td>
                    <td style='padding:8px;border-bottom:1px solid #eee;
                        text-align:center'>{i.Quantity}</td>
                    <td style='padding:8px;border-bottom:1px solid #eee;
                        text-align:right'>₦{i.TotalPrice:N2}</td>
                </tr>"));

            var body = $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Arial,sans-serif;max-width:600px;
                margin:0 auto;padding:20px'>
                <div style='background:#6C63FF;padding:20px;
                    border-radius:8px 8px 0 0;text-align:center'>
                    <h1 style='color:white;margin:0'>
                        Order Confirmed! 🎉
                    </h1>
                </div>
                <div style='background:#f9f9f9;padding:20px;
                    border-radius:0 0 8px 8px'>
                    <p>Hi <strong>{order.CustomerName}</strong>,</p>
                    <p>Your order from <strong>{order.StoreName}</strong>
                        has been confirmed!</p>

                    <div style='background:white;padding:15px;
                        border-radius:8px;margin:15px 0'>
                        <h3 style='margin:0 0 10px;color:#6C63FF'>
                            Order #{order.OrderNumber}
                        </h3>
                        <table style='width:100%;border-collapse:collapse'>
                            <thead>
                                <tr style='background:#f5f5f5'>
                                    <th style='padding:8px;text-align:left'>
                                        Product</th>
                                    <th style='padding:8px;text-align:center'>
                                        Qty</th>
                                    <th style='padding:8px;text-align:right'>
                                        Price</th>
                                </tr>
                            </thead>
                            <tbody>{itemsHtml}</tbody>
                            <tfoot>
                                <tr>
                                    <td colspan='2' style='padding:8px;
                                        font-weight:bold'>Total</td>
                                    <td style='padding:8px;text-align:right;
                                        font-weight:bold;color:#6C63FF'>
                                        ₦{order.Total:N2}</td>
                                </tr>
                            </tfoot>
                        </table>
                    </div>

                    <p>We'll notify you when your order ships.</p>

                    {(!string.IsNullOrEmpty(order.StoreWhatsApp)
                        ? $@"<p>Questions? Chat with us on WhatsApp:
                            <a href='https://wa.me/{order.StoreWhatsApp}'>
                            {order.StoreWhatsApp}</a></p>"
                        : "")}

                    <p style='color:#888;font-size:12px;margin-top:20px'>
                        Powered by Vendify
                    </p>
                </div>
            </body>
            </html>";

            return await SendEmailAsync(new SendEmailRequest
            {
                ToEmail = order.CustomerEmail,
                ToName = order.CustomerName,
                Subject = $"Order Confirmed - #{order.OrderNumber} | {order.StoreName}",
                Body = body
            });
        }

        public async Task<bool> SendOrderStatusUpdateAsync(OrderEmailDto order)
        {
            var statusEmoji = order.Status switch
            {
                "Processing" => "⚙️",
                "Shipped" => "🚚",
                "Delivered" => "✅",
                "Cancelled" => "❌",
                _ => "📦"
            };

            var trackingSection = !string.IsNullOrEmpty(order.TrackingNumber)
                ? $@"<div style='background:#e8f5e9;padding:12px;
                    border-radius:6px;margin:15px 0'>
                    <strong>Tracking Number:</strong>
                    {order.TrackingNumber}
                    </div>"
                : "";

            var body = $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Arial,sans-serif;max-width:600px;
                margin:0 auto;padding:20px'>
                <div style='background:#6C63FF;padding:20px;
                    border-radius:8px 8px 0 0;text-align:center'>
                    <h1 style='color:white;margin:0'>
                        Order Update {statusEmoji}
                    </h1>
                </div>
                <div style='background:#f9f9f9;padding:20px;
                    border-radius:0 0 8px 8px'>
                    <p>Hi <strong>{order.CustomerName}</strong>,</p>
                    <p>Your order <strong>#{order.OrderNumber}</strong>
                        status has been updated to:
                        <strong style='color:#6C63FF'>
                            {order.Status}
                        </strong>
                    </p>
                    {trackingSection}
                    <p>Thank you for shopping with
                        <strong>{order.StoreName}</strong>!</p>
                    <p style='color:#888;font-size:12px;margin-top:20px'>
                        Powered by Vendify
                    </p>
                </div>
            </body>
            </html>";

            return await SendEmailAsync(new SendEmailRequest
            {
                ToEmail = order.CustomerEmail,
                ToName = order.CustomerName,
                Subject = $"Order #{order.OrderNumber} - {order.Status} {statusEmoji}",
                Body = body
            });
        }

        public async Task<bool> SendPasswordResetEmailAsync(
            string email, string name, string resetToken)
        {
            var resetUrl =
                $"{_configuration["AppUrl"]}/reset-password?token={resetToken}&email={email}";

            var body = $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Arial,sans-serif;max-width:600px;
                margin:0 auto;padding:20px'>
                <div style='background:#6C63FF;padding:20px;
                    border-radius:8px 8px 0 0;text-align:center'>
                    <h1 style='color:white;margin:0'>
                        Reset Your Password 🔐
                    </h1>
                </div>
                <div style='background:#f9f9f9;padding:20px;
                    border-radius:0 0 8px 8px'>
                    <p>Hi <strong>{name}</strong>,</p>
                    <p>We received a request to reset your password.
                        Click the button below to proceed:</p>
                    <div style='text-align:center;margin:25px 0'>
                        <a href='{resetUrl}'
                            style='background:#6C63FF;color:white;
                            padding:12px 30px;border-radius:6px;
                            text-decoration:none;font-weight:bold'>
                            Reset Password
                        </a>
                    </div>
                    <p style='color:#888'>
                        This link expires in 2 hours.
                        If you didn't request this, ignore this email.
                    </p>
                    <p style='color:#888;font-size:12px;margin-top:20px'>
                        Powered by Vendify
                    </p>
                </div>
            </body>
            </html>";

            return await SendEmailAsync(new SendEmailRequest
            {
                ToEmail = email,
                ToName = name,
                Subject = "Reset Your Vendify Password",
                Body = body
            });
        }

        public async Task<bool> SendWelcomeEmailAsync(
            string email, string name)
        {
            var body = $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Arial,sans-serif;max-width:600px;
                margin:0 auto;padding:20px'>
                <div style='background:#6C63FF;padding:20px;
                    border-radius:8px 8px 0 0;text-align:center'>
                    <h1 style='color:white;margin:0'>
                        Welcome to Vendify! 🚀
                    </h1>
                </div>
                <div style='background:#f9f9f9;padding:20px;
                    border-radius:0 0 8px 8px'>
                    <p>Hi <strong>{name}</strong>,</p>
                    <p>Welcome to Vendify! You're just a few steps away
                        from launching your online store.</p>
                    <h3>Getting Started:</h3>
                    <ol>
                        <li>Create your store</li>
                        <li>Add your products</li>
                        <li>Set up shipping zones</li>
                        <li>Connect your payment method</li>
                        <li>Share your store link!</li>
                    </ol>
                    <p>Need help? We're always here for you.</p>
                    <p style='color:#888;font-size:12px;margin-top:20px'>
                        Powered by Vendify
                    </p>
                </div>
            </body>
            </html>";

            return await SendEmailAsync(new SendEmailRequest
            {
                ToEmail = email,
                ToName = name,
                Subject = "Welcome to Vendify! 🚀",
                Body = body
            });
        }

        // ── WHATSAPP ─────────────────────────────────────────

        public async Task<bool> SendWhatsAppMessageAsync(
            WhatsAppMessageDto message)
        {
            try
            {
                var accessToken =
                    _configuration["WhatsApp:AccessToken"];
                var phoneNumberId =
                    _configuration["WhatsApp:PhoneNumberId"];

                if (string.IsNullOrEmpty(accessToken) ||
                    string.IsNullOrEmpty(phoneNumberId))
                {
                    Console.WriteLine(
                        "WhatsApp not configured — skipping");
                    return false;
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                // Clean phone number
                var phone = message.PhoneNumber
                    .Replace("+", "")
                    .Replace("-", "")
                    .Replace(" ", "");

                // Add country code if missing (Nigeria default)
                if (phone.StartsWith("0"))
                    phone = "234" + phone.Substring(1);

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = phone,
                    type = "text",
                    text = new { body = message.Message }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"https://graph.facebook.com/v18.0/{phoneNumberId}/messages",
                    content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WhatsApp error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendOrderWhatsAppNotificationAsync(
            OrderEmailDto order, string merchantWhatsApp)
        {
            var message = $@"🛍️ *New Order Alert!*

Order: *#{order.OrderNumber}*
Customer: {order.CustomerName}
Phone: {order.CustomerEmail}

*Items:*
{string.Join("\n", order.Items.Select(i =>
    $"• {i.ProductName} x{i.Quantity} = ₦{i.TotalPrice:N2}"))}

*Total: ₦{order.Total:N2}*

Login to your Vendify dashboard to process this order.";

            return await SendWhatsAppMessageAsync(new WhatsAppMessageDto
            {
                PhoneNumber = merchantWhatsApp,
                Message = message
            });
        }
    }
}