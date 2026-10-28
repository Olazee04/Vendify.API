using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Customers;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class CustomerAuthService : ICustomerAuthService
    {
        private const string PurposeVerify = "Verify";
        private const string PurposeReset = "Reset";

        // How long a sign-in lasts. Change the number to adjust.
        private const int SessionHours = 24;

        private const int Iterations = 210_000;
        private const int MaxFailedLogins = 5;
        private const int LockMinutes = 15;

        // Used so a wrong email takes as long as a wrong password
        private static readonly string DummyHash =
            HashPassword("not-a-real-password-1");

        private sealed record IssueResult(bool Throttled, string? MailError);

        private readonly VendifyDbContext _context;
        private readonly IConfiguration _config;
        private readonly ICustomerMailer _mailer;
        private readonly ILogger<CustomerAuthService> _logger;

        public CustomerAuthService(
            VendifyDbContext context,
            IConfiguration config,
            ICustomerMailer mailer,
            ILogger<CustomerAuthService> logger)
        {
            _context = context;
            _config = config;
            _mailer = mailer;
            _logger = logger;
        }

        // In development, error messages include the technical reason
        private bool IsDev => string.Equals(
            _config["ASPNETCORE_ENVIRONMENT"], "Development",
            StringComparison.OrdinalIgnoreCase);

        private string MailFailureMessage(string? reason) =>
            "We could not send the email right now. Please try again in a few minutes." +
            (IsDev && !string.IsNullOrWhiteSpace(reason)
                ? $" [Developer note: {reason}]"
                : "");

        // ═══════════════ REGISTER ════════════════════════

        public async Task<ApiResponse<bool>> RegisterAsync(
            string storeSlug, CustomerRegisterRequest request)
        {
            var store = await FindStoreAsync(storeSlug);
            if (store == null) return Fail<bool>("Store not found");

            var name = (request.Name ?? "").Trim();
            if (name.Length < 2 || name.Length > 100)
                return Fail<bool>("Please enter your full name.");

            var email = NormalizeEmail(request.Email);
            if (email == null)
                return Fail<bool>("Please enter a valid email address.");

            var phone = (request.Phone ?? "").Trim();
            if (phone.Count(char.IsDigit) < 10 || phone.Length > 20)
                return Fail<bool>("Please enter a valid phone number.");

            var pwError = CheckPasswordRules(request.Password);
            if (pwError != null) return Fail<bool>(pwError);

            var customer = await _context.StoreCustomers
                .FirstOrDefaultAsync(c =>
                    c.StoreId == store.Id && c.Email == email);

            if (customer != null && customer.IsEmailVerified)
                return Fail<bool>(
                    "An account with this email already exists. Please sign in.");

            if (customer == null)
            {
                customer = new StoreCustomer
                {
                    StoreId = store.Id,
                    Email = email
                };
                _context.StoreCustomers.Add(customer);
            }

            // An unverified account can be overwritten. Nobody can use it
            // until the owner of the email enters the code.
            customer.Name = name;
            customer.Phone = phone;
            customer.PasswordHash = HashPassword(request.Password);
            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var issued = await IssueCodeAsync(
                customer, PurposeVerify, store.Name);

            if (issued.MailError != null)
                return Fail<bool>(MailFailureMessage(issued.MailError));

            return ApiResponse<bool>.SuccessResponse(true,
                $"We sent a 6-digit code to {email}. " +
                "Enter it to finish creating your account.");
        }

        public async Task<ApiResponse<CustomerSessionDto>> VerifyEmailAsync(
            string storeSlug, CustomerVerifyRequest request)
        {
            const string bad =
                "That code is not correct or has expired. Please request a new code.";

            var store = await FindStoreAsync(storeSlug);
            if (store == null)
                return Fail<CustomerSessionDto>("Store not found");

            var email = NormalizeEmail(request.Email);
            if (email == null) return Fail<CustomerSessionDto>(bad);

            var customer = await _context.StoreCustomers
                .FirstOrDefaultAsync(c =>
                    c.StoreId == store.Id && c.Email == email);

            if (customer == null) return Fail<CustomerSessionDto>(bad);

            if (customer.IsEmailVerified)
                return Fail<CustomerSessionDto>(
                    "This email is already verified. Please sign in.");

            if (!await CheckCodeAsync(customer, PurposeVerify, request.Code))
                return Fail<CustomerSessionDto>(bad);

            var now = DateTime.UtcNow;
            customer.IsEmailVerified = true;
            customer.FailedLoginCount = 0;
            customer.LockedUntil = null;
            customer.LastLoginAt = now;
            customer.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return ApiResponse<CustomerSessionDto>.SuccessResponse(
                CreateSession(customer), "Email verified. Welcome!");
        }

        public async Task<ApiResponse<bool>> ResendVerificationAsync(
            string storeSlug, string? emailRaw)
        {
            const string generic =
                "If that email has an account waiting to be verified, " +
                "we have sent a new 6-digit code.";

            var store = await FindStoreAsync(storeSlug);
            if (store == null) return Fail<bool>("Store not found");

            var email = NormalizeEmail(emailRaw);
            if (email == null)
                return Fail<bool>("Please enter a valid email address.");

            var customer = await _context.StoreCustomers
                .FirstOrDefaultAsync(c =>
                    c.StoreId == store.Id && c.Email == email);

            if (customer != null && !customer.IsEmailVerified)
            {
                var issued = await IssueCodeAsync(
                    customer, PurposeVerify, store.Name);

                // Only developers see a failure here; shoppers always
                // get the same message so emails cannot be looked up.
                if (issued.MailError != null && IsDev)
                    return Fail<bool>(MailFailureMessage(issued.MailError));
            }

            return ApiResponse<bool>.SuccessResponse(true, generic);
        }

        // ═══════════════ SIGN IN ═════════════════════════

        public async Task<ApiResponse<CustomerSessionDto>> LoginAsync(
            string storeSlug, CustomerLoginRequest request)
        {
            const string bad = "Incorrect email or password.";

            var store = await FindStoreAsync(storeSlug);
            if (store == null)
                return Fail<CustomerSessionDto>("Store not found");

            var email = NormalizeEmail(request.Email);
            var password = request.Password ?? "";
            if (email == null || password.Length == 0 || password.Length > 200)
                return Fail<CustomerSessionDto>(bad);

            var customer = await _context.StoreCustomers
                .FirstOrDefaultAsync(c =>
                    c.StoreId == store.Id && c.Email == email);

            if (customer == null)
            {
                // Same effort as a real check, so timing gives nothing away
                _ = VerifyPassword(password, DummyHash);
                return Fail<CustomerSessionDto>(bad);
            }

            var now = DateTime.UtcNow;
            if (customer.LockedUntil.HasValue && customer.LockedUntil.Value > now)
            {
                var mins = (int)Math.Ceiling(
                    (customer.LockedUntil.Value - now).TotalMinutes);
                return Fail<CustomerSessionDto>(
                    $"Too many wrong attempts. Please try again in {mins} minute(s), or reset your password.");
            }

            if (!VerifyPassword(password, customer.PasswordHash))
            {
                await RecordFailedLoginAsync(customer);
                return Fail<CustomerSessionDto>(bad);
            }

            if (!customer.IsEmailVerified)
            {
                await IssueCodeAsync(customer, PurposeVerify, store.Name);
                return Fail<CustomerSessionDto>(CustomerMessages.NotVerified);
            }

            customer.FailedLoginCount = 0;
            customer.LockedUntil = null;
            customer.LastLoginAt = now;
            customer.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return ApiResponse<CustomerSessionDto>.SuccessResponse(
                CreateSession(customer), "Signed in");
        }

        // ═══════════════ FORGOT / RESET ══════════════════

        public async Task<ApiResponse<bool>> ForgotPasswordAsync(
            string storeSlug, string? emailRaw)
        {
            const string generic =
                "If that email has an account, we have sent a 6-digit code " +
                "to reset the password. It is valid for 10 minutes.";

            var store = await FindStoreAsync(storeSlug);
            if (store == null) return Fail<bool>("Store not found");

            var email = NormalizeEmail(emailRaw);
            if (email == null)
                return Fail<bool>("Please enter a valid email address.");

            var customer = await _context.StoreCustomers
                .FirstOrDefaultAsync(c =>
                    c.StoreId == store.Id && c.Email == email);

            if (customer != null && customer.IsEmailVerified)
            {
                var issued = await IssueCodeAsync(
                    customer, PurposeReset, store.Name);

                if (issued.MailError != null && IsDev)
                    return Fail<bool>(MailFailureMessage(issued.MailError));
            }

            return ApiResponse<bool>.SuccessResponse(true, generic);
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(
            string storeSlug, CustomerResetRequest request)
        {
            const string bad =
                "That code is not correct or has expired. Please request a new code.";

            var store = await FindStoreAsync(storeSlug);
            if (store == null) return Fail<bool>("Store not found");

            var pwError = CheckPasswordRules(request.NewPassword);
            if (pwError != null) return Fail<bool>(pwError);

            var email = NormalizeEmail(request.Email);
            if (email == null) return Fail<bool>(bad);

            var customer = await _context.StoreCustomers
                .FirstOrDefaultAsync(c =>
                    c.StoreId == store.Id && c.Email == email);

            if (customer == null || !customer.IsEmailVerified)
                return Fail<bool>(bad);

            if (!await CheckCodeAsync(customer, PurposeReset, request.Code))
                return Fail<bool>(bad);

            // A new password also signs the customer out everywhere
            customer.PasswordHash = HashPassword(request.NewPassword);
            customer.FailedLoginCount = 0;
            customer.LockedUntil = null;
            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true,
                "Your password has been changed. Please sign in.");
        }

        public async Task<ApiResponse<CustomerSessionDto>> ChangePasswordAsync(
            string storeSlug, CustomerChangePasswordRequest request)
        {
            var identity = await ResolveAsync(storeSlug, request.Token);
            if (identity == null)
                return Fail<CustomerSessionDto>(CustomerMessages.SessionExpired);

            var pwError = CheckPasswordRules(request.NewPassword);
            if (pwError != null) return Fail<CustomerSessionDto>(pwError);

            var customer = await _context.StoreCustomers
                .FirstAsync(c => c.Id == identity.CustomerId);

            var now = DateTime.UtcNow;
            if (customer.LockedUntil.HasValue && customer.LockedUntil.Value > now)
                return Fail<CustomerSessionDto>(
                    "Too many wrong attempts. Please try again later.");

            if (!VerifyPassword(request.CurrentPassword ?? "", customer.PasswordHash))
            {
                await RecordFailedLoginAsync(customer);
                return Fail<CustomerSessionDto>(
                    "Your current password is not correct.");
            }

            customer.PasswordHash = HashPassword(request.NewPassword);
            customer.FailedLoginCount = 0;
            customer.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return ApiResponse<CustomerSessionDto>.SuccessResponse(
                CreateSession(customer), "Password changed.");
        }

        // ═══════════════ MY ACCOUNT ══════════════════════

        public async Task<ApiResponse<CustomerProfileDto>> GetProfileAsync(
            string storeSlug, string? token)
        {
            var identity = await ResolveAsync(storeSlug, token);
            if (identity == null)
                return Fail<CustomerProfileDto>(CustomerMessages.SessionExpired);

            return ApiResponse<CustomerProfileDto>.SuccessResponse(
                new CustomerProfileDto
                {
                    Name = identity.Name,
                    Email = identity.Email,
                    Phone = identity.Phone
                });
        }

        public async Task<ApiResponse<List<CustomerOrderSummaryDto>>>
            GetOrdersAsync(string storeSlug, string? token)
        {
            var identity = await ResolveAsync(storeSlug, token);
            if (identity == null)
                return Fail<List<CustomerOrderSummaryDto>>(
                    CustomerMessages.SessionExpired);

            var email = identity.Email;
            var storeId = identity.StoreId;

            var rows = await _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreId == storeId &&
                            o.CustomerEmail.ToLower() == email)
                .OrderByDescending(o => o.CreatedAt)
                .Take(50)
                .Select(o => new
                {
                    o.OrderNumber,
                    o.CreatedAt,
                    o.Status,
                    o.PaymentStatus,
                    o.Total,
                    Items = o.Items.Sum(i => i.Quantity)
                })
                .ToListAsync();

            var list = rows.Select(r => new CustomerOrderSummaryDto
            {
                OrderNumber = r.OrderNumber,
                CreatedAt = r.CreatedAt,
                Status = r.Status.ToString(),
                PaymentStatus = r.PaymentStatus.ToString(),
                Total = r.Total,
                ItemCount = r.Items
            }).ToList();

            return ApiResponse<List<CustomerOrderSummaryDto>>
                .SuccessResponse(list);
        }

        // Works out who is signed in from their token
        public async Task<CustomerIdentity?> ResolveAsync(
            string storeSlug, string? token)
        {
            var parsed = ParseToken(token);
            if (parsed == null) return null;

            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());
            if (store == null || store.Id != parsed.Value.StoreId) return null;

            var c = await _context.StoreCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == parsed.Value.CustomerId && x.StoreId == store.Id);

            if (c == null || !c.IsEmailVerified) return null;

            // The stamp changes whenever the password changes
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(Stamp(c)),
                    Encoding.UTF8.GetBytes(parsed.Value.Stamp)))
                return null;

            return new CustomerIdentity
            {
                CustomerId = c.Id,
                StoreId = c.StoreId,
                Email = c.Email,
                Name = c.Name,
                Phone = c.Phone
            };
        }

        // ═══════════════ HELPERS ═════════════════════════

        private static ApiResponse<T> Fail<T>(string message) =>
            ApiResponse<T>.FailureResponse(message);

        private Task<Store?> FindStoreAsync(string storeSlug) =>
            _context.Stores.FirstOrDefaultAsync(
                s => s.Slug == storeSlug.ToLower());

        private async Task RecordFailedLoginAsync(StoreCustomer customer)
        {
            customer.FailedLoginCount++;
            if (customer.FailedLoginCount >= MaxFailedLogins)
            {
                customer.LockedUntil = DateTime.UtcNow.AddMinutes(LockMinutes);
                customer.FailedLoginCount = 0;
            }
            await _context.SaveChangesAsync();
        }

        // Sends a code. Throttled = asked for too often.
        private async Task<IssueResult> IssueCodeAsync(
            StoreCustomer c, string purpose, string storeName)
        {
            var now = DateTime.UtcNow;

            var recent = await _context.CustomerAuthCodes
                .Where(x => x.CustomerId == c.Id &&
                            x.Purpose == purpose &&
                            x.CreatedAt > now.AddHours(-1))
                .Select(x => x.CreatedAt)
                .ToListAsync();

            // One code a minute, five an hour
            if (recent.Any(t => t > now.AddSeconds(-60)) || recent.Count >= 5)
                return new IssueResult(true, null);

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            _context.CustomerAuthCodes.Add(new CustomerAuthCode
            {
                StoreId = c.StoreId,
                CustomerId = c.Id,
                Purpose = purpose,
                CodeHash = HashCode(c.StoreId, c.Id, purpose, code),
                ExpiresAt = now.AddMinutes(10)
            });
            await _context.SaveChangesAsync();

            var mailError = await _mailer.SendCodeAsync(
                c.Email, storeName, purpose, code);

            if (mailError != null)
                _logger.LogError(
                    "Could not email a {Purpose} code to {Email}: {Error}",
                    purpose, c.Email, mailError);

            return new IssueResult(false, mailError);
        }

        private async Task<bool> CheckCodeAsync(
            StoreCustomer c, string purpose, string? code)
        {
            code = (code ?? "").Trim();
            if (code.Length != 6 || !code.All(char.IsDigit)) return false;

            var now = DateTime.UtcNow;
            var record = await _context.CustomerAuthCodes
                .Where(x => x.CustomerId == c.Id &&
                            x.Purpose == purpose &&
                            !x.IsUsed &&
                            x.ExpiresAt > now)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (record == null || record.Attempts >= 5) return false;

            var expected = HashCode(c.StoreId, c.Id, purpose, code);
            var match = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(record.CodeHash));

            if (!match)
            {
                record.Attempts++;
                await _context.SaveChangesAsync();
                return false;
            }

            record.IsUsed = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ── Passwords ──

        private static string? CheckPasswordRules(string? password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return "Your password must be at least 8 characters.";
            if (password.Length > 100)
                return "Your password is too long (100 characters at most).";
            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
                return "Your password must contain at least one letter and one number.";
            return null;
        }

        private static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA512, 32);
            return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string stored)
        {
            try
            {
                var parts = stored.Split('.');
                if (parts.Length != 4 || parts[0] != "v1") return false;
                if (!int.TryParse(parts[1], out var iterations)) return false;

                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    password, salt, iterations,
                    HashAlgorithmName.SHA512, expected.Length);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        // ── Codes and tokens ──

        private byte[] GetKey()
        {
            var secret = _config["JwtSettings:Secret"]
                ?? Environment.GetEnvironmentVariable("JWT_SECRET");
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException(
                    "JwtSettings:Secret is not configured.");

            return SHA256.HashData(
                Encoding.UTF8.GetBytes("vendify-customer:" + secret));
        }

        private string HashCode(
            Guid storeId, Guid customerId, string purpose, string code)
        {
            var data = Encoding.UTF8.GetBytes(
                $"{storeId:N}\n{customerId:N}\n{purpose}\n{code}");
            return Convert.ToHexString(HMACSHA256.HashData(GetKey(), data));
        }

        private string Stamp(StoreCustomer c) =>
            Convert.ToHexString(HMACSHA256.HashData(
                GetKey(), Encoding.UTF8.GetBytes("stamp:" + c.PasswordHash)))
                [..16];

        private CustomerSessionDto CreateSession(StoreCustomer c)
        {
            var expires = DateTime.UtcNow.AddHours(SessionHours);
            var exp = new DateTimeOffset(expires, TimeSpan.Zero)
                .ToUnixTimeSeconds();

            var payload = Encoding.UTF8.GetBytes(
                $"{c.Id:N}\n{c.StoreId:N}\n{exp}\n{Stamp(c)}");
            var signature = HMACSHA256.HashData(GetKey(), payload);

            return new CustomerSessionDto
            {
                Token = ToB64(payload) + "." + ToB64(signature),
                ExpiresAt = expires,
                Name = c.Name,
                Email = c.Email,
                Phone = c.Phone
            };
        }

        private (Guid CustomerId, Guid StoreId, string Stamp)? ParseToken(
            string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 1000)
                return null;

            var parts = token.Split('.');
            if (parts.Length != 2) return null;

            byte[] payload, signature;
            try
            {
                payload = FromB64(parts[0]);
                signature = FromB64(parts[1]);
            }
            catch
            {
                return null;
            }

            var expected = HMACSHA256.HashData(GetKey(), payload);
            if (!CryptographicOperations.FixedTimeEquals(expected, signature))
                return null;

            var f = Encoding.UTF8.GetString(payload).Split('\n');
            if (f.Length != 4) return null;
            if (!Guid.TryParseExact(f[0], "N", out var customerId)) return null;
            if (!Guid.TryParseExact(f[1], "N", out var storeId)) return null;
            if (!long.TryParse(f[2], out var exp)) return null;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;

            return (customerId, storeId, f[3]);
        }

        private static string ToB64(byte[] data) =>
            Convert.ToBase64String(data)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromB64(string s)
        {
            var b64 = s.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            return Convert.FromBase64String(b64);
        }

        private static string? NormalizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var e = email.Trim().ToLower();
            try
            {
                var addr = new System.Net.Mail.MailAddress(e);
                return addr.Address == e ? e : null;
            }
            catch
            {
                return null;
            }
        }
    }
}