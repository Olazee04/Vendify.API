using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Stockpile;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class StockpileService : IStockpileService
    {
        private const string StatusOpen = "Open";
        private const string StatusCheckingOut = "CheckingOut";
        private const string StatusCheckedOut = "CheckedOut";

        private readonly VendifyDbContext _context;
        private readonly IPaymentVerificationService _paymentVerifier;
        private readonly IConfiguration _config;
        private readonly IStockpileMailer _mailer;
        private readonly ILogger<StockpileService> _logger;
        private readonly IStockpileNotifier _notifier;
        private readonly ICustomerAuthService _customers;

        public StockpileService(
            VendifyDbContext context,
            IPaymentVerificationService paymentVerifier,
            IConfiguration config,
            IStockpileMailer mailer,
            ILogger<StockpileService> logger,
            IStockpileNotifier notifier,
            ICustomerAuthService customers)
        {
            _context = context;
            _paymentVerifier = paymentVerifier;
            _config = config;
            _mailer = mailer;
            _logger = logger;
            _notifier = notifier;
            _customers = customers;
        }

        // ═══════════════ MERCHANT SETTINGS ═══════════════

        public async Task<ApiResponse<StockpileSettingsDto>>
            GetSettingsAsync(Guid storeId)
        {
            var settings = await _context.StockpileSettings
                .FirstOrDefaultAsync(s => s.StoreId == storeId);

            return ApiResponse<StockpileSettingsDto>.SuccessResponse(
                MapToDto(settings));
        }

        public async Task<ApiResponse<StockpileSettingsDto>>
            UpdateSettingsAsync(
                Guid storeId, UpdateStockpileSettingsRequest request)
        {
            if (request.MaxItems.HasValue && request.MaxItems.Value < 1)
                return ApiResponse<StockpileSettingsDto>.FailureResponse(
                    "Maximum items must be at least 1.");

            if (request.MaxAmount.HasValue && request.MaxAmount.Value <= 0)
                return ApiResponse<StockpileSettingsDto>.FailureResponse(
                    "Maximum amount must be greater than 0.");

            if (request.MaxWeightKg.HasValue &&
                request.MaxWeightKg.Value <= 0)
                return ApiResponse<StockpileSettingsDto>.FailureResponse(
                    "Maximum weight must be greater than 0.");

            if (request.HoldDays.HasValue &&
                (request.HoldDays.Value < 1 || request.HoldDays.Value > 365))
                return ApiResponse<StockpileSettingsDto>.FailureResponse(
                    "Hold days must be between 1 and 365.");

            if (request.IsEnabled &&
                !request.MaxItems.HasValue &&
                !request.MaxAmount.HasValue &&
                !request.MaxWeightKg.HasValue)
                return ApiResponse<StockpileSettingsDto>.FailureResponse(
                    "Set at least one limit (items, amount or weight) " +
                    "before turning stockpile on.");

            var settings = await _context.StockpileSettings
                .FirstOrDefaultAsync(s => s.StoreId == storeId);

            if (settings == null)
            {
                settings = new StockpileSettings { StoreId = storeId };
                _context.StockpileSettings.Add(settings);
            }

            settings.IsEnabled = request.IsEnabled;
            settings.MaxItems = request.MaxItems;
            settings.MaxAmount = request.MaxAmount;
            settings.MaxWeightKg = request.MaxWeightKg;
            settings.HoldDays = request.HoldDays;
            settings.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ApiResponse<StockpileSettingsDto>.SuccessResponse(
                MapToDto(settings), "Stockpile settings saved");
        }

        // ═══════════════ CUSTOMER: ADD TO STOCKPILE ══════

        public async Task<ApiResponse<StockpilePublicInfoDto>>
            GetPublicInfoAsync(string storeSlug)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());

            if (store == null)
                return ApiResponse<StockpilePublicInfoDto>.FailureResponse(
                    "Store not found");

            var s = await _context.StockpileSettings
                .FirstOrDefaultAsync(x => x.StoreId == store.Id);

            return ApiResponse<StockpilePublicInfoDto>.SuccessResponse(
                new StockpilePublicInfoDto
                {
                    IsEnabled = s?.IsEnabled ?? false,
                    MaxItems = s?.MaxItems,
                    MaxAmount = s?.MaxAmount,
                    MaxWeightKg = s?.MaxWeightKg,
                    HoldDays = s?.HoldDays
                });
        }

        // Runs BEFORE the customer pays, so nobody pays and is then refused
        public async Task<ApiResponse<StockpileCheckDto>> CheckAsync(
            string storeSlug, StockpileCheckRequest request)
        {
            var session = await ResolveSessionAsync(storeSlug, request.Token);
            if (session.Error != null)
                return ApiResponse<StockpileCheckDto>.FailureResponse(
                    session.Error);

            var prep = await PrepareAsync(
                storeSlug, session.Email, request.Items);

            if (prep.Error != null)
                return ApiResponse<StockpileCheckDto>.FailureResponse(
                    prep.Error);

            return ApiResponse<StockpileCheckDto>.SuccessResponse(
                new StockpileCheckDto
                {
                    Amount = prep.Amount,
                    ItemCount = prep.ItemCount,
                    WillBeFull = prep.WillBeFull
                });
        }

        public async Task<ApiResponse<StockpileDepositResultDto>>
        DepositAsync(string storeSlug, StockpileDepositRequest request)
        {
            // Only signed-in customers can stockpile
            var session = await ResolveSessionAsync(storeSlug, request.Token);
            if (session.Error != null)
                return ApiResponse<StockpileDepositResultDto>
                    .FailureResponse(session.Error);

            if (string.IsNullOrWhiteSpace(request.PaymentReference))
                return ApiResponse<StockpileDepositResultDto>
                    .FailureResponse("A payment reference is required.");

            var prep = await PrepareAsync(
                storeSlug, session.Email, request.Items);

            if (prep.Error != null)
                return ApiResponse<StockpileDepositResultDto>
                    .FailureResponse(prep.Error);

            var reference = request.PaymentReference.Trim();

            var alreadyUsed =
                await _context.StockpileDeposits
                    .AnyAsync(d => d.PaymentReference == reference) ||
                await _context.Orders
                    .AnyAsync(o => o.PaymentReference == reference);

            if (alreadyUsed)
                return ApiResponse<StockpileDepositResultDto>
                    .FailureResponse(
                        "This payment has already been used.");

            // Ask Paystack / Flutterwave if the payment is real
            var check = await _paymentVerifier.VerifyAsync(
                request.PaymentMethod, reference, prep.Amount);

            if (!check.IsVerified)
                return ApiResponse<StockpileDepositResultDto>
                    .FailureResponse(check.Message);

            var now = DateTime.UtcNow;
            var stockpile = prep.Existing;
            var isNewStockpile = stockpile == null;

            if (stockpile == null)
            {
                stockpile = new Stockpile
                {
                    StoreId = prep.Store!.Id,
                    CustomerName = session.Name,
                    CustomerEmail = prep.Email,
                    CustomerPhone = session.Phone,
                    Status = StatusOpen,
                    ExpiresAt = prep.Settings!.HoldDays.HasValue
                        ? now.AddDays(prep.Settings.HoldDays.Value)
                        : null
                };
            }
            else
            {
                stockpile.CustomerName = session.Name;
                stockpile.CustomerPhone = session.Phone;
                stockpile.UpdatedAt = now;
            }

            var deposit = new StockpileDeposit
            {
                StockpileId = stockpile.Id,
                PaymentMethod = request.PaymentMethod,
                PaymentReference = reference,
                AmountPaid = prep.Amount
            };

            try
            {
                // Save in a safe order inside ONE transaction: the
                // stockpile first, then the payment, then the items and
                // stock. If anything fails, nothing is kept.
                await using var tx =
                    await _context.Database.BeginTransactionAsync();

                if (isNewStockpile)
                    _context.Stockpiles.Add(stockpile);
                await _context.SaveChangesAsync();

                _context.StockpileDeposits.Add(deposit);
                await _context.SaveChangesAsync();

                foreach (var line in prep.Lines)
                {
                    var p = line.Item;

                    _context.StockpileItems.Add(new StockpileItem
                    {
                        StockpileId = stockpile.Id,
                        DepositId = deposit.Id,
                        ProductId = p.Id,
                        ProductName = p.Name,
                        ProductImageUrl =
                            p.Images.FirstOrDefault(i => i.IsPrimary)?.Url
                            ?? p.Images.FirstOrDefault()?.Url,
                        Quantity = line.Qty,
                        UnitPrice = p.Price,
                        TotalPrice = p.Price * line.Qty,
                        WeightKg = p.Weight
                    });

                    // Reserve the stock so nobody else can buy it
                    if (p.TrackInventory)
                        p.StockQuantity -= line.Qty;
                }
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                var reason = ex.InnerException?.Message ?? ex.Message;

                _logger.LogError(ex,
                    "Stockpile deposit could not be saved for payment {Reference}",
                    reference);

                var isDev = string.Equals(
                    _config["ASPNETCORE_ENVIRONMENT"], "Development",
                    StringComparison.OrdinalIgnoreCase);

                return ApiResponse<StockpileDepositResultDto>
                    .FailureResponse(
                        "We could not save your stockpile. " +
                        "Please contact the store with your payment reference." +
                        (isDev ? $" [Developer note: {reason}]" : ""));
            }

            _notifier.NotifyDeposit(
                prep.Store!.Id,
                stockpile.CustomerName,
                stockpile.CustomerEmail,
                prep.Amount,
                prep.ItemCount,
                prep.WillBeFull);

            return ApiResponse<StockpileDepositResultDto>.SuccessResponse(
                new StockpileDepositResultDto
                {
                    AmountPaid = prep.Amount,
                    ItemsAdded = prep.ItemCount,
                    IsFull = prep.WillBeFull
                },
                prep.WillBeFull
                    ? "Added to your stockpile. Your stockpile is now full, so check it out to receive your items."
                    : "Added to your stockpile!");
        }

        // ═══════════════ CUSTOMER: EMAIL ACCESS ══════════

        public async Task<ApiResponse<bool>> RequestAccessAsync(
            string storeSlug, StockpileAccessRequest request)
        {
            // The same message is returned whether or not the email has
            // a stockpile, so nobody can use this to look up customers.
            const string generic =
                "If that email has an open stockpile, we have sent a 6-digit " +
                "code to it. The code is valid for 10 minutes.";

            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());
            if (store == null)
                return ApiResponse<bool>.FailureResponse("Store not found");

            var email = NormalizeEmail(request.Email);
            if (email == null)
                return ApiResponse<bool>.FailureResponse(
                    "Please enter a valid email address.");

            var storeId = store.Id;

            var hasOpen = await _context.Stockpiles.AnyAsync(s =>
                s.StoreId == storeId &&
                s.CustomerEmail == email &&
                s.Status == StatusOpen);

            if (!hasOpen)
                return ApiResponse<bool>.SuccessResponse(true, generic);

            var now = DateTime.UtcNow;
            var lastMinute = now.AddSeconds(-60);
            var lastHour = now.AddHours(-1);

            var recent = await _context.StockpileAccessCodes
                .Where(c => c.StoreId == storeId &&
                            c.CustomerEmail == email &&
                            c.CreatedAt > lastHour)
                .Select(c => c.CreatedAt)
                .ToListAsync();

            // One code a minute, five an hour
            if (recent.Any(t => t > lastMinute) || recent.Count >= 5)
                return ApiResponse<bool>.SuccessResponse(true, generic);

            var code = RandomNumberGenerator
                .GetInt32(100000, 1000000).ToString();

            _context.StockpileAccessCodes.Add(new StockpileAccessCode
            {
                StoreId = storeId,
                CustomerEmail = email,
                CodeHash = HashCode(storeId, email, code),
                ExpiresAt = now.AddMinutes(10)
            });
            await _context.SaveChangesAsync();

            var sent = await _mailer.SendAccessCodeAsync(
                email, store.Name, code);
            if (!sent)
                _logger.LogError(
                    "Stockpile code could not be emailed to {Email}", email);

            return ApiResponse<bool>.SuccessResponse(true, generic);
        }

        public async Task<ApiResponse<StockpileTokenDto>> VerifyAccessAsync(
            string storeSlug, StockpileVerifyRequest request)
        {
            const string bad =
                "That code is not correct or has expired. " +
                "Please request a new code.";

            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());
            if (store == null)
                return Fail<StockpileTokenDto>("Store not found");

            var email = NormalizeEmail(request.Email);
            if (email == null)
                return Fail<StockpileTokenDto>(
                    "Please enter a valid email address.");

            var code = (request.Code ?? "").Trim();
            if (code.Length != 6 || !code.All(char.IsDigit))
                return Fail<StockpileTokenDto>(bad);

            var storeId = store.Id;
            var now = DateTime.UtcNow;

            var record = await _context.StockpileAccessCodes
                .Where(c => c.StoreId == storeId &&
                            c.CustomerEmail == email &&
                            !c.IsUsed &&
                            c.ExpiresAt > now)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (record == null)
                return Fail<StockpileTokenDto>(bad);

            if (record.Attempts >= 5)
                return Fail<StockpileTokenDto>(
                    "Too many wrong attempts. Please request a new code.");

            var expected = HashCode(storeId, email, code);
            var ok = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(record.CodeHash));

            if (!ok)
            {
                record.Attempts++;
                await _context.SaveChangesAsync();
                return Fail<StockpileTokenDto>(bad);
            }

            record.IsUsed = true;
            await _context.SaveChangesAsync();

            var expires = now.AddMinutes(60);
            return ApiResponse<StockpileTokenDto>.SuccessResponse(
                new StockpileTokenDto
                {
                    Token = CreateToken(storeId, email, expires),
                    ExpiresAt = expires
                });
        }

        // ═══════════════ CUSTOMER: VIEW ══════════════════

        public async Task<ApiResponse<StockpileViewDto>> GetMineAsync(
            string storeSlug, StockpileViewRequest request)
        {
            var session = await ResolveSessionAsync(storeSlug, request.Token);
            if (session.Error != null)
                return Fail<StockpileViewDto>(session.Error);

            var storeId = session.Store!.Id;
            var email = session.Email;

            var stockpile = await _context.Stockpiles
                .FirstOrDefaultAsync(s =>
                    s.StoreId == storeId &&
                    s.CustomerEmail == email &&
                    s.Status == StatusOpen);

            if (stockpile == null)
                return Fail<StockpileViewDto>(
                    "You have no open stockpile at this store.");

            var items = await _context.StockpileItems
                .Where(i => i.StockpileId == stockpile.Id)
                .OrderBy(i => i.CreatedAt)
                .ToListAsync();

            var settings = await _context.StockpileSettings
                .FirstOrDefaultAsync(s => s.StoreId == storeId);

            var dto = new StockpileViewDto
            {
                Id = stockpile.Id,
                CustomerName = stockpile.CustomerName,
                CustomerEmail = stockpile.CustomerEmail,
                CustomerPhone = stockpile.CustomerPhone,
                ExpiresAt = stockpile.ExpiresAt,
                IsExpired = stockpile.ExpiresAt.HasValue &&
                            stockpile.ExpiresAt.Value < DateTime.UtcNow,
                Items = items.Select(i => new StockpileViewItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    ProductImageUrl = i.ProductImageUrl,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    WeightKg = i.WeightKg
                }).ToList(),
                TotalItems = items.Sum(i => i.Quantity),
                TotalAmount = items.Sum(i => i.TotalPrice),
                TotalWeightKg = items.Sum(i => (i.WeightKg ?? 0m) * i.Quantity),
                MaxItems = settings?.MaxItems,
                MaxAmount = settings?.MaxAmount,
                MaxWeightKg = settings?.MaxWeightKg
            };

            return ApiResponse<StockpileViewDto>.SuccessResponse(dto);
        }

        // ═══════════════ CUSTOMER: CHECKOUT ══════════════

        public async Task<ApiResponse<StockpileCheckoutResultDto>>
            CheckoutAsync(string storeSlug, StockpileCheckoutRequest request)
        {
            var session = await ResolveSessionAsync(storeSlug, request.Token);
            if (session.Error != null)
                return Fail<StockpileCheckoutResultDto>(session.Error);

            var store = session.Store!;
            var storeId = store.Id;
            var email = session.Email;

            // 1. Delivery details
            var addr = request.ShippingAddress;
            if (addr == null ||
                string.IsNullOrWhiteSpace(addr.FullName) ||
                string.IsNullOrWhiteSpace(addr.PhoneNumber) ||
                string.IsNullOrWhiteSpace(addr.AddressLine1) ||
                string.IsNullOrWhiteSpace(addr.City) ||
                string.IsNullOrWhiteSpace(addr.State))
                return Fail<StockpileCheckoutResultDto>(
                    "Please fill in your full delivery details.");

            var country = string.IsNullOrWhiteSpace(addr.Country)
                ? "Nigeria" : addr.Country.Trim();

            // 2. The open stockpile
            var stockpile = await _context.Stockpiles
                .FirstOrDefaultAsync(s =>
                    s.StoreId == storeId &&
                    s.CustomerEmail == email &&
                    s.Status == StatusOpen);

            if (stockpile == null)
                return Fail<StockpileCheckoutResultDto>(
                    "You have no open stockpile at this store.");

            if (stockpile.ExpiresAt.HasValue &&
                stockpile.ExpiresAt.Value < DateTime.UtcNow)
                return Fail<StockpileCheckoutResultDto>(
                    "Your stockpile has expired. Please contact the store.");

            var stockpileId = stockpile.Id;

            var items = await _context.StockpileItems
                .Where(i => i.StockpileId == stockpileId)
                .ToListAsync();

            if (items.Count == 0)
                return Fail<StockpileCheckoutResultDto>(
                    "Your stockpile is empty.");

            var itemsTotal = items.Sum(i => i.TotalPrice);

            // 3. Shipping fee, always worked out on the server
            decimal fee = 0m;
            var zones = await _context.ShippingZones
                .Where(z => z.StoreId == storeId && z.IsActive)
                .ToListAsync();

            if (zones.Any())
            {
                var zone = request.ShippingZoneId.HasValue
                    ? ShippingZoneMatcher.FindById(
                        zones, request.ShippingZoneId.Value,
                        addr.State, country)
                    : ShippingZoneMatcher.FindZone(
                        zones, addr.State, country, addr.City);

                if (zone == null)
                    return Fail<StockpileCheckoutResultDto>(
                        $"Sorry, this store does not deliver to {addr.State} yet.");

                fee = ShippingZoneMatcher.FeeOf(zone);
            }

            // 4. Shipping payment (only if there is a fee)
            string? paidReference = null;
            if (fee > 0m)
            {
                paidReference = request.PaymentReference?.Trim();

                if (string.IsNullOrWhiteSpace(paidReference))
                    return Fail<StockpileCheckoutResultDto>(
                        $"Please pay the shipping fee of ₦{fee:N0} to check out.");

                var alreadyUsed =
                    await _context.Orders
                        .AnyAsync(o => o.PaymentReference == paidReference) ||
                    await _context.StockpileDeposits
                        .AnyAsync(d => d.PaymentReference == paidReference);

                if (alreadyUsed)
                    return Fail<StockpileCheckoutResultDto>(
                        "This payment has already been used.");

                var check = await _paymentVerifier.VerifyAsync(
                    request.PaymentMethod, paidReference, fee);

                if (!check.IsVerified)
                    return Fail<StockpileCheckoutResultDto>(check.Message);
            }

            // 5. Claim the stockpile so it can only be checked out once
            var claimed = await _context.Stockpiles
                .Where(s => s.Id == stockpileId && s.Status == StatusOpen)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.Status, StatusCheckingOut));

            if (claimed == 0)
                return Fail<StockpileCheckoutResultDto>(
                    "This stockpile has already been checked out.");

            try
            {
                var depositCount = await _context.StockpileDeposits
                    .CountAsync(d => d.StockpileId == stockpileId);

                var now = DateTime.UtcNow;

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = await GenerateOrderNumberAsync(),
                    CustomerName = stockpile.CustomerName,
                    CustomerEmail = stockpile.CustomerEmail,
                    CustomerPhone = stockpile.CustomerPhone,
                    Status = OrderStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentMethod = request.PaymentMethod,
                    PaymentReference = paidReference,
                    Subtotal = itemsTotal,
                    ShippingFee = fee,
                    Discount = 0m,
                    Total = itemsTotal + fee,
                    Notes = $"Stockpile checkout. Items were paid in {depositCount} earlier payment(s).",
                    StoreId = storeId,
                    ShippingAddress = new Vendify.Core.Entities.ShippingAddress
                    {
                        FullName = addr.FullName.Trim(),
                        PhoneNumber = addr.PhoneNumber.Trim(),
                        AddressLine1 = addr.AddressLine1.Trim(),
                        AddressLine2 = addr.AddressLine2,
                        City = addr.City.Trim(),
                        State = addr.State.Trim(),
                        Country = country,
                        PostalCode = addr.PostalCode
                    },
                    Items = items.Select(i => new OrderItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice,
                        ProductImageUrl = i.ProductImageUrl
                    }).ToList()
                };

                _context.Orders.Add(order);

                // Count the sales (stock was already reserved at deposit)
                var productIds = items.Select(i => i.ProductId)
                    .Distinct().ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();
                foreach (var item in items)
                {
                    var p = products.FirstOrDefault(x => x.Id == item.ProductId);
                    if (p != null) p.SalesCount += item.Quantity;
                }

                stockpile.Status = StatusCheckedOut;
                stockpile.OrderId = order.Id;
                stockpile.CheckedOutAt = now;
                stockpile.UpdatedAt = now;

                await _context.SaveChangesAsync();

                _notifier.NotifyCheckout(
    storeId,
    order.OrderNumber,
    stockpile.CustomerName,
    itemsTotal + fee);

                return ApiResponse<StockpileCheckoutResultDto>.SuccessResponse(
                    new StockpileCheckoutResultDto
                    {
                        OrderNumber = order.OrderNumber,
                        ItemsTotal = itemsTotal,
                        ShippingFee = fee,
                        OrderTotal = itemsTotal + fee
                    },
                    "Checkout complete! Your items will be shipped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Stockpile checkout failed for stockpile {Id}", stockpileId);

                // Put the stockpile back so the customer can try again
                await _context.Stockpiles
                    .Where(s => s.Id == stockpileId)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(x => x.Status, StatusOpen));

                return Fail<StockpileCheckoutResultDto>(
                    "We could not complete your checkout. " +
                    (paidReference != null
                        ? "Please contact the store with your payment reference."
                        : "Please try again."));
            }
        }

        // ═══════════════ SHARED HELPERS ══════════════════

        private static ApiResponse<T> Fail<T>(string message) =>
            ApiResponse<T>.FailureResponse(message);

        private class Session
        {
            public string? Error { get; set; }
            public Store? Store { get; set; }
            public string Email { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
        }

        // Works out which signed-in customer this is
        private async Task<Session> ResolveSessionAsync(
            string storeSlug, string? token)
        {
            var s = new Session();

            var identity = await _customers.ResolveAsync(storeSlug, token);
            if (identity == null)
            {
                s.Error = StockpileMessages.SessionExpired;
                return s;
            }

            var store = await _context.Stores
                .FirstOrDefaultAsync(x => x.Id == identity.StoreId);
            if (store == null)
            {
                s.Error = StockpileMessages.SessionExpired;
                return s;
            }

            s.Store = store;
            s.Email = identity.Email;
            s.Name = identity.Name;
            s.Phone = identity.Phone;
            return s;
        }

        // The signing key is derived from your JWT secret
        private byte[] GetKey()
        {
            var secret = _config["JwtSettings:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException(
                    "JwtSettings:Secret is not configured.");

            return SHA256.HashData(
                Encoding.UTF8.GetBytes("vendify-stockpile:" + secret));
        }

        private string HashCode(Guid storeId, string email, string code)
        {
            var data = Encoding.UTF8.GetBytes($"{storeId:N}\n{email}\n{code}");
            return Convert.ToHexString(HMACSHA256.HashData(GetKey(), data));
        }

        private string CreateToken(Guid storeId, string email, DateTime expiresUtc)
        {
            var exp = new DateTimeOffset(expiresUtc, TimeSpan.Zero)
                .ToUnixTimeSeconds();
            var payload = Encoding.UTF8.GetBytes($"{storeId:N}\n{email}\n{exp}");
            var signature = HMACSHA256.HashData(GetKey(), payload);
            return Base64Url(payload) + "." + Base64Url(signature);
        }

        private (Guid StoreId, string Email)? ValidateToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var parts = token.Split('.');
            if (parts.Length != 2) return null;

            byte[] payload, signature;
            try
            {
                payload = FromBase64Url(parts[0]);
                signature = FromBase64Url(parts[1]);
            }
            catch
            {
                return null;
            }

            var expected = HMACSHA256.HashData(GetKey(), payload);
            if (!CryptographicOperations.FixedTimeEquals(expected, signature))
                return null;

            var fields = Encoding.UTF8.GetString(payload).Split('\n');
            if (fields.Length != 3) return null;
            if (!Guid.TryParseExact(fields[0], "N", out var storeId)) return null;
            if (!long.TryParse(fields[2], out var exp)) return null;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;

            return (storeId, fields[1]);
        }

        private static string Base64Url(byte[] data) =>
            Convert.ToBase64String(data)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromBase64Url(string s)
        {
            var b64 = s.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            return Convert.FromBase64String(b64);
        }

        private async Task<string> GenerateOrderNumberAsync()
        {
            var today = DateTime.UtcNow;
            var prefix = $"VND-{today:yyyyMMdd}";
            var count = await _context.Orders
                .CountAsync(o => o.OrderNumber.StartsWith(prefix));
            return $"{prefix}-{(count + 1):D4}";
        }

        // ═══════════════ ADD-TO-STOCKPILE CHECKS ═════════

        private class Prepared
        {
            public string? Error { get; set; }
            public Store? Store { get; set; }
            public StockpileSettings? Settings { get; set; }
            public Stockpile? Existing { get; set; }
            public string Email { get; set; } = string.Empty;
            public List<(Product Item, int Qty)> Lines { get; set; } = new();
            public decimal Amount { get; set; }
            public int ItemCount { get; set; }
            public bool WillBeFull { get; set; }
        }

        private async Task<Prepared> PrepareAsync(
            string storeSlug,
            string? customerEmail,
            List<StockpileItemRequest>? items)
        {
            var r = new Prepared();

            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());
            if (store == null)
            {
                r.Error = "Store not found";
                return r;
            }
            r.Store = store;

            var settings = await _context.StockpileSettings
                .FirstOrDefaultAsync(s => s.StoreId == store.Id);
            if (settings == null || !settings.IsEnabled)
            {
                r.Error = "Stockpile is not available for this store.";
                return r;
            }
            r.Settings = settings;

            var email = NormalizeEmail(customerEmail);
            if (email == null)
            {
                r.Error = "Please enter a valid email address.";
                return r;
            }
            r.Email = email;

            if (items == null || items.Count == 0)
            {
                r.Error = "Your cart is empty.";
                return r;
            }

            // Combine repeated products and validate quantities
            var wanted = new Dictionary<Guid, int>();
            foreach (var item in items)
            {
                if (!Guid.TryParse(item.ProductId, out var productId))
                {
                    r.Error = "There is an invalid product in your cart.";
                    return r;
                }
                if (item.Quantity < 1 || item.Quantity > 1000)
                {
                    r.Error = "Invalid quantity in your cart.";
                    return r;
                }
                wanted.TryGetValue(productId, out var current);
                wanted[productId] = current + item.Quantity;
            }

            // Load the real products (price and stock come from here)
            var ids = wanted.Keys.ToList();
            var products = await _context.Products
                .Include(p => p.Images)
                .Where(p => ids.Contains(p.Id) &&
                            p.StoreId == store.Id &&
                            p.IsPublished)
                .ToListAsync();

            if (products.Count != ids.Count)
            {
                r.Error = "One or more products are no longer available.";
                return r;
            }

            decimal addWeight = 0m;
            foreach (var p in products)
            {
                var qty = wanted[p.Id];

                if (p.IsDigital)
                {
                    r.Error = $"\"{p.Name}\" is a digital product and can't be stockpiled.";
                    return r;
                }

                if (p.TrackInventory && p.StockQuantity < qty)
                {
                    r.Error = $"Insufficient stock for {p.Name}.";
                    return r;
                }

                if (settings.MaxWeightKg.HasValue &&
                    (!p.Weight.HasValue || p.Weight.Value <= 0))
                {
                    r.Error = $"\"{p.Name}\" has no weight set, so it can't be stockpiled yet.";
                    return r;
                }

                r.Lines.Add((p, qty));
                r.Amount += p.Price * qty;
                r.ItemCount += qty;
                addWeight += (p.Weight ?? 0m) * qty;
            }

            // The customer's existing open stockpile (if any)
            var stockpile = await _context.Stockpiles
                .FirstOrDefaultAsync(s =>
                    s.StoreId == store.Id &&
                    s.CustomerEmail == email &&
                    s.Status == StatusOpen);
            r.Existing = stockpile;

            if (stockpile?.ExpiresAt != null &&
                stockpile.ExpiresAt < DateTime.UtcNow)
            {
                r.Error = "Your earlier stockpile has expired. " +
                          "Please contact the store.";
                return r;
            }

            int existingCount = 0;
            decimal existingAmount = 0m, existingWeight = 0m;

            if (stockpile != null)
            {
                var existingItems = await _context.StockpileItems
                    .Where(i => i.StockpileId == stockpile.Id)
                    .ToListAsync();

                existingCount = existingItems.Sum(i => i.Quantity);
                existingAmount = existingItems.Sum(i => i.TotalPrice);
                existingWeight = existingItems.Sum(
                    i => (i.WeightKg ?? 0m) * i.Quantity);
            }

            var newCount = existingCount + r.ItemCount;
            var newAmount = existingAmount + r.Amount;
            var newWeight = existingWeight + addWeight;

            const string fullMsg =
                "Your stockpile is full. Please check out your stockpile before adding more.";

            // The first limit reached stops further stockpiling
            if (settings.MaxItems.HasValue &&
                newCount > settings.MaxItems.Value)
            {
                var left = Math.Max(0, settings.MaxItems.Value - existingCount);
                r.Error = left == 0
                    ? fullMsg
                    : $"This would go over the stockpile limit of {settings.MaxItems.Value} items. You can add up to {left} more item(s).";
                return r;
            }

            if (settings.MaxAmount.HasValue &&
                newAmount > settings.MaxAmount.Value)
            {
                var left = Math.Max(0m, settings.MaxAmount.Value - existingAmount);
                r.Error = left <= 0m
                    ? fullMsg
                    : $"This would go over the stockpile limit of ₦{settings.MaxAmount.Value:N0}. You can add up to ₦{left:N0} more.";
                return r;
            }

            if (settings.MaxWeightKg.HasValue &&
                newWeight > settings.MaxWeightKg.Value)
            {
                var left = Math.Max(0m, settings.MaxWeightKg.Value - existingWeight);
                r.Error = left <= 0m
                    ? fullMsg
                    : $"This would go over the stockpile weight limit of {settings.MaxWeightKg.Value:0.##} kg. You can add up to {left:0.##} kg more.";
                return r;
            }

            r.WillBeFull =
                (settings.MaxItems.HasValue &&
                    newCount >= settings.MaxItems.Value) ||
                (settings.MaxAmount.HasValue &&
                    newAmount >= settings.MaxAmount.Value) ||
                (settings.MaxWeightKg.HasValue &&
                    newWeight >= settings.MaxWeightKg.Value);

            return r;
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

        private static StockpileSettingsDto MapToDto(StockpileSettings? s)
        {
            return new StockpileSettingsDto
            {
                IsEnabled = s?.IsEnabled ?? false,
                MaxItems = s?.MaxItems,
                MaxAmount = s?.MaxAmount,
                MaxWeightKg = s?.MaxWeightKg,
                HoldDays = s?.HoldDays
            };
        }
    }
}