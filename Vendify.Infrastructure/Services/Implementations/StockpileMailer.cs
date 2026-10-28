using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Vendify.Application.Services.Interfaces;

namespace Vendify.Infrastructure.Services.Implementations
{
    // Sends the one-time stockpile code using the same email
    // settings ("Email" section) your OTP emails already use.
    public class StockpileMailer : IStockpileMailer
    {
        private readonly IConfiguration _config;
        private readonly ILogger<StockpileMailer> _logger;

        public StockpileMailer(
            IConfiguration config, ILogger<StockpileMailer> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendAccessCodeAsync(
            string toEmail, string storeName, string code)
        {
            try
            {
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
                {
                    _logger.LogError("Email settings are missing.");
                    return false;
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = $"Your {storeName} stockpile code";
                message.Body = new TextPart("plain")
                {
                    Text =
                        $"Your code to open your stockpile at {storeName} is:\n\n" +
                        $"    {code}\n\n" +
                        "It is valid for 10 minutes. If you did not ask for " +
                        "this code, you can ignore this email."
                };

                using var client = new SmtpClient();
                await client.ConnectAsync(
                    host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(user, pass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send stockpile code email to {Email}", toEmail);
                return false;
            }
        }
    }
}