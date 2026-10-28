using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Vendify.Application.Services.Interfaces;

namespace Vendify.Infrastructure.Services.Implementations
{
    // Uses the same "Email" settings as your other emails
    public class CustomerMailer : ICustomerMailer
    {
        private readonly IConfiguration _config;
        private readonly ILogger<CustomerMailer> _logger;

        public CustomerMailer(
            IConfiguration config, ILogger<CustomerMailer> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<string?> SendCodeAsync(
            string toEmail, string storeName, string purpose, string code)
        {
            try
            {
                var host = _config["Email:Host"];
                var portText = _config["Email:Port"] ?? "587";
                var user = _config["Email:Username"];
                var pass = _config["Email:Password"];
                var fromEmail = _config["Email:FromEmail"] ?? user;
                var fromName = _config["Email:FromName"] ?? "Vendify";

                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(host)) missing.Add("Email:Host");
                if (string.IsNullOrWhiteSpace(user)) missing.Add("Email:Username");
                if (string.IsNullOrWhiteSpace(pass)) missing.Add("Email:Password");
                if (string.IsNullOrWhiteSpace(fromEmail)) missing.Add("Email:FromEmail");
                if (missing.Count > 0)
                    return "Email settings are missing: " +
                           string.Join(", ", missing) + ".";

                if (!int.TryParse(portText, out var port))
                    return "Email:Port is not a number.";

                var isReset = purpose == "Reset";
                var subject = isReset
                    ? $"Reset your {storeName} password"
                    : $"Verify your email for {storeName}";
                var intro = isReset
                    ? $"Use this code to reset your password at {storeName}:"
                    : $"Use this code to verify your email and finish creating your account at {storeName}:";

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;
                message.Body = new TextPart("plain")
                {
                    Text =
                        $"{intro}\n\n    {code}\n\n" +
                        "It is valid for 10 minutes. If you did not ask for " +
                        "this, you can ignore this email."
                };

                using var client = new SmtpClient();
                client.Timeout = 20000; // 20 seconds, so it never hangs
                await client.ConnectAsync(
                    host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(user, pass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                return null;
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                _logger.LogError(ex, "Email login was rejected");
                return "The email server rejected the login (" + ex.Message +
                       "). For Gmail this usually means the app password " +
                       "is wrong or was revoked.";
            }
            catch (SmtpCommandException ex)
            {
                _logger.LogError(ex, "Email server refused the message");
                return $"The email server refused the message ({ex.StatusCode}): {ex.Message}";
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Could not reach the email server");
                return "Could not reach the email server. Your network may be blocking port 587.";
            }
            catch (Exception ex) when (
                ex is TimeoutException || ex is OperationCanceledException)
            {
                _logger.LogError(ex, "Email server took too long");
                return "The email server took too long to answer. Your network may be blocking it.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send customer code email to {Email}", toEmail);
                return ex.GetType().Name + ": " + ex.Message;
            }
        }
    }
}