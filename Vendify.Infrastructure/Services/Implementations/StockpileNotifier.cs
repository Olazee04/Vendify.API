using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using Vendify.Application.Services.Interfaces;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    // Emails the merchant. It sends in the background, so customers
    // never wait for the email, and a failed email never breaks a payment.
    public class StockpileNotifier : IStockpileNotifier
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<StockpileNotifier> _logger;

        public StockpileNotifier(
            IServiceScopeFactory scopes,
            IConfiguration config,
            ILogger<StockpileNotifier> logger)
        {
            _scopes = scopes;
            _config = config;
            _logger = logger;
        }

        public void NotifyDeposit(
            Guid storeId, string customerName, string customerEmail,
            decimal amount, int itemCount, bool isFull)
        {
            var body =
                $"{customerName} ({customerEmail}) paid ₦{amount:N0} " +
                $"for {itemCount} item(s) into their stockpile.\n\n" +
                (isFull
                    ? "Their stockpile is now full, so they should check out soon.\n\n"
                    : "") +
                "Open Stockpile, then Customer stockpiles, in your Vendify " +
                "dashboard to see the details.";

            Send(storeId, "New stockpile payment", body);
        }

        public void NotifyCheckout(
            Guid storeId, string orderNumber,
            string customerName, decimal orderTotal)
        {
            var body =
                $"{customerName} has checked out their stockpile.\n\n" +
                $"Order {orderNumber}, total ₦{orderTotal:N0}, " +
                "is ready to ship.\n\n" +
                "Open Orders in your Vendify dashboard to ship it.";

            Send(storeId, $"Stockpile checked out ({orderNumber})", body);
        }

        public void NotifyPaymentProblem(
    Guid storeId, string reference, string kind, string reason)
        {
            var what = kind == "Order" ? "an order payment"
                : kind == "StockpileDeposit" ? "a stockpile payment"
                : "a stockpile delivery payment";

            var body =
                $"A customer's payment for {what} could not be recorded automatically.\n\n" +
                $"Payment reference: {reference}\n" +
                $"Reason: {reason}\n\n" +
                "What to do:\n" +
                "1. Search this reference in your Paystack or Flutterwave dashboard to see whether the money arrived.\n" +
                "2. Open 'Payments needing attention' in your Vendify dashboard. Press Try again, or refund the customer and mark it resolved.";

            Send(storeId, "A payment needs your attention", body);
        }
        private void Send(Guid storeId, string subject, string body)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider
                        .GetRequiredService<VendifyDbContext>();

                    var store = await db.Stores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == storeId);
                    if (store == null) return;

                    // Support email if set, otherwise the owner's login email
                    var to = store.SupportEmail;
                    if (string.IsNullOrWhiteSpace(to))
                    {
                        to = await db.Users
                            .AsNoTracking()
                            .Where(u => u.Id == store.UserId)
                            .Select(u => u.Email)
                            .FirstOrDefaultAsync();
                    }
                    if (string.IsNullOrWhiteSpace(to)) return;

                    var host = _config["Email:Host"];
                    var port = int.Parse(_config["Email:Port"] ?? "587");
                    var user = _config["Email:Username"];
                    var pass = _config["Email:Password"];
                    var fromEmail = _config["Email:FromEmail"] ?? user;
                    var fromName = _config["Email:FromName"] ?? "Vendify";

                    if (string.IsNullOrWhiteSpace(host) ||
                        string.IsNullOrWhiteSpace(user) ||
                        string.IsNullOrWhiteSpace(pass) ||
                        string.IsNullOrWhiteSpace(fromEmail))
                        return;

                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress(fromName, fromEmail));
                    message.To.Add(MailboxAddress.Parse(to));
                    message.Subject = $"[{store.Name}] {subject}";
                    message.Body = new TextPart("plain") { Text = body };

                    using var client = new SmtpClient();
                    await client.ConnectAsync(
                        host, port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(user, pass);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not send merchant stockpile notification");
                }
            });
        }
    }
}