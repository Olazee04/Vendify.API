using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Order;
using Vendify.Application.DTOs.Payment;
using Vendify.Application.DTOs.Stockpile;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class PaymentFulfilmentService : IPaymentFulfilmentService
    {
        private const string Pending = "Pending";
        private const string Processing = "Processing";
        private const string Fulfilled = "Fulfilled";
        private const string Failed = "Failed";

        private const string KindOrder = "Order";
        private const string KindDeposit = "StockpileDeposit";
        private const string KindCheckout = "StockpileCheckout";

        private const int MaxAttempts = 5;

        private static readonly JsonSerializerOptions Json =
            new(JsonSerializerDefaults.Web);

        private readonly VendifyDbContext _context;
        private readonly IOrderService _orderService;
        private readonly IStockpileService _stockpileService;
        private readonly ILogger<PaymentFulfilmentService> _logger;
        private readonly IStockpileNotifier _notifier;

        public PaymentFulfilmentService(
            VendifyDbContext context,
            IOrderService orderService,
            IStockpileService stockpileService,
            ILogger<PaymentFulfilmentService> logger,
            IStockpileNotifier notifier)
        {
            _context = context;
            _orderService = orderService;
            _stockpileService = stockpileService;
            _logger = logger;
            _notifier = notifier;
        }

        // ═══════════ 1. SAVE THE DETAILS BEFORE PAYING ═══════════

        public async Task<ApiResponse<PaymentIntentDto>> CreateIntentAsync(
            string storeSlug, CreatePaymentIntentRequest request)
        {
            var kind = (request.Kind ?? "").Trim();
            if (kind != KindOrder && kind != KindDeposit && kind != KindCheckout)
                return ApiResponse<PaymentIntentDto>.FailureResponse(
                    "Unknown payment type.");

            if (request.Payload.ValueKind != JsonValueKind.Object)
                return ApiResponse<PaymentIntentDto>.FailureResponse(
                    "Missing payment details.");

            var raw = request.Payload.GetRawText();
            if (raw.Length > 20000)
                return ApiResponse<PaymentIntentDto>.FailureResponse(
                    "Payment details are too large.");

            // Make sure the details can be read as the right kind
            try
            {
                object? parsed = kind switch
                {
                    KindOrder => JsonSerializer.Deserialize<CreateOrderRequest>(raw, Json),
                    KindDeposit => JsonSerializer.Deserialize<StockpileDepositRequest>(raw, Json),
                    _ => JsonSerializer.Deserialize<StockpileCheckoutRequest>(raw, Json),
                };
                if (parsed == null)
                    return ApiResponse<PaymentIntentDto>.FailureResponse(
                        "Invalid payment details.");
            }
            catch (JsonException)
            {
                return ApiResponse<PaymentIntentDto>.FailureResponse(
                    "Invalid payment details.");
            }

            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());
            if (store == null)
                return ApiResponse<PaymentIntentDto>.FailureResponse(
                    "Store not found");

            var prefix = kind == KindOrder ? "ORD"
                       : kind == KindDeposit ? "STK" : "SHP";
            var reference = $"VND-{prefix}-{Guid.NewGuid():N}";

            _context.PaymentIntents.Add(new PaymentIntent
            {
                Reference = reference,
                StoreId = store.Id,
                Kind = kind,
                PayloadJson = raw,
                Status = Pending
            });
            await _context.SaveChangesAsync();

            return ApiResponse<PaymentIntentDto>.SuccessResponse(
                new PaymentIntentDto { Reference = reference });
        }

        // ═══════════ 2. RECORD IT AFTER PAYMENT ══════════════════

        public async Task<FulfilResult> FulfilAsync(string reference)
        {
            reference = (reference ?? "").Trim();
            if (reference.Length == 0 || reference.Length > 100)
                return FulfilResult.Missing();

            var intent = await _context.PaymentIntents
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Reference == reference);

            if (intent == null)
                return FulfilResult.Missing();

            if (intent.Status == Fulfilled) return FromStored(intent);
            if (intent.Status == Failed) return FromFailed(intent);
            if (intent.Status == "Resolved") return FromFailed(intent);

            // Claim it, so only one caller does the work.
            // A claim stuck for over 2 minutes is treated as abandoned.
            var now = DateTime.UtcNow;
            var stale = now.AddMinutes(-2);

            var claimed = await _context.PaymentIntents
                .Where(i => i.Id == intent.Id &&
                            (i.Status == Pending ||
                             (i.Status == Processing && i.UpdatedAt < stale)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, Processing)
                    .SetProperty(x => x.UpdatedAt, now));

            if (claimed == 0)
            {
                // Someone else is working on it. Wait for them.
                for (var n = 0; n < 30; n++)
                {
                    await Task.Delay(500);
                    var cur = await _context.PaymentIntents
                        .AsNoTracking()
                        .FirstAsync(i => i.Id == intent.Id);

                    if (cur.Status == Fulfilled) return FromStored(cur);
                    if (cur.Status == Failed) return FromFailed(cur);
                    if (cur.Status == Pending) break;
                }

                return new FulfilResult
                {
                    Success = false,
                    Retry = true,
                    Message = "We are still confirming your payment. " +
                              "Please check again in a moment."
                };
            }

            FulfilResult result;
            try
            {
                result = await AlreadyRecordedAsync(reference, intent)
                         ?? await RunAsync(intent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Recording payment {Reference} threw an error", reference);
                result = FulfilResult.Fail(
                    "Something went wrong while recording your payment.");
            }

            var done = DateTime.UtcNow;

            if (result.Success)
            {
                result.Data!.Message = result.Message;
                var resultJson = JsonSerializer.Serialize(result.Data, Json);

                await _context.PaymentIntents
                    .Where(i => i.Id == intent.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, Fulfilled)
                        .SetProperty(x => x.FulfilledAt, (DateTime?)done)
                        .SetProperty(x => x.UpdatedAt, done)
                        .SetProperty(x => x.ResultJson, (string?)resultJson)
                        .SetProperty(x => x.FailureReason, (string?)null));

                return result;
            }

            var attempts = intent.Attempts + 1;
            var giveUp = result.Permanent || attempts >= MaxAttempts;
            var reason = result.Message.Length > 500
                ? result.Message.Substring(0, 500) : result.Message;

            await _context.PaymentIntents
                .Where(i => i.Id == intent.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, giveUp ? Failed : Pending)
                    .SetProperty(x => x.Attempts, attempts)
                    .SetProperty(x => x.FailureReason, (string?)reason)
                    .SetProperty(x => x.UpdatedAt, done));

            if (giveUp)
            {
                _logger.LogError(
                    "PAYMENT NEEDS ATTENTION: {Reference} ({Kind}) could not " +
                    "be recorded: {Reason}", reference, intent.Kind, reason);

                _notifier.NotifyPaymentProblem(
                    intent.StoreId, reference, intent.Kind, reason);
            }

            result.Retry = !giveUp;
            return result;
        }

        // ═══════════ HELPERS ═════════════════════════════════════

        // If the order / deposit already exists for this reference
        // (for example the old direct route recorded it), just say so.
        private async Task<FulfilResult?> AlreadyRecordedAsync(
            string reference, PaymentIntent intent)
        {
            var orderNumber = await _context.Orders
                .AsNoTracking()
                .Where(o => o.PaymentReference == reference)
                .Select(o => o.OrderNumber)
                .FirstOrDefaultAsync();

            var depositExists = orderNumber == null &&
                await _context.StockpileDeposits
                    .AsNoTracking()
                    .AnyAsync(d => d.PaymentReference == reference);

            if (orderNumber == null && !depositExists) return null;

            return FulfilResult.Ok(
                new PaymentFulfilDto
                {
                    Kind = intent.Kind,
                    OrderNumber = orderNumber
                },
                "Your payment has already been recorded.");
        }

        private async Task<FulfilResult> RunAsync(PaymentIntent intent)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == intent.StoreId);

            if (store == null)
                return FulfilResult.Fail("Store not found", permanent: true);

            switch (intent.Kind)
            {
                case KindOrder:
                    {
                        var req = JsonSerializer.Deserialize<CreateOrderRequest>(
                            intent.PayloadJson, Json);
                        if (req == null)
                            return FulfilResult.Fail("Invalid order details.", true);

                        req.PaymentMethod = ChosenProvider(req.PaymentMethod);
                        req.PaymentReference = intent.Reference;

                        var r = await _orderService.CreateOrderAsync(req, store.Slug);
                        if (!r.Success)
                            return FulfilResult.Fail(r.Message ?? "Order failed.");

                        return FulfilResult.Ok(
                            new PaymentFulfilDto
                            {
                                Kind = intent.Kind,
                                OrderNumber = r.Data?.OrderNumber,
                                ItemsTotal = r.Data?.Subtotal,
                                ShippingFee = r.Data?.ShippingFee,
                                OrderTotal = r.Data?.Total
                            },
                            "Order placed");
                    }

                case KindDeposit:
                    {
                        var req = JsonSerializer.Deserialize<StockpileDepositRequest>(
                            intent.PayloadJson, Json);
                        if (req == null)
                            return FulfilResult.Fail("Invalid stockpile details.", true);

                        req.PaymentMethod = ChosenProvider(req.PaymentMethod);
                        req.PaymentReference = intent.Reference;

                        var r = await _stockpileService.DepositAsync(store.Slug, req);
                        if (!r.Success)
                            return FulfilResult.Fail(r.Message ?? "Deposit failed.");

                        return FulfilResult.Ok(
                            new PaymentFulfilDto { Kind = intent.Kind },
                            r.Message ?? "Added to your stockpile!");
                    }

                case KindCheckout:
                    {
                        var req = JsonSerializer.Deserialize<StockpileCheckoutRequest>(
                            intent.PayloadJson, Json);
                        if (req == null)
                            return FulfilResult.Fail("Invalid checkout details.", true);

                        req.PaymentMethod = ChosenProvider(req.PaymentMethod);
                        req.PaymentReference = intent.Reference;

                        var r = await _stockpileService.CheckoutAsync(store.Slug, req);
                        if (!r.Success)
                        {
                            var expired = r.Message == StockpileMessages.SessionExpired;
                            return FulfilResult.Fail(
                                r.Message ?? "Checkout failed.", permanent: expired);
                        }

                        return FulfilResult.Ok(
                            new PaymentFulfilDto
                            {
                                Kind = intent.Kind,
                                OrderNumber = r.Data?.OrderNumber,
                                ItemsTotal = r.Data?.ItemsTotal,
                                ShippingFee = r.Data?.ShippingFee,
                                OrderTotal = r.Data?.OrderTotal
                            },
                            r.Message ?? "Checkout complete!");
                    }

                default:
                    return FulfilResult.Fail("Unknown payment type.", true);
            }
        }

        // The customer's payment provider: Flutterwave, otherwise Paystack
        private static PaymentMethod ChosenProvider(PaymentMethod chosen) =>
            chosen == PaymentMethod.Flutterwave
                ? PaymentMethod.Flutterwave
                : PaymentMethod.Paystack;
        private static FulfilResult FromStored(PaymentIntent intent)
        {
            PaymentFulfilDto? dto = null;
            if (!string.IsNullOrWhiteSpace(intent.ResultJson))
            {
                try
                {
                    dto = JsonSerializer.Deserialize<PaymentFulfilDto>(
                        intent.ResultJson, Json);
                }
                catch (JsonException) { }
            }
            dto ??= new PaymentFulfilDto { Kind = intent.Kind };

            return FulfilResult.Ok(
                dto, dto.Message ?? "Your payment has been recorded.");
        }

        private static FulfilResult FromFailed(PaymentIntent intent) =>
            FulfilResult.Fail(
                (intent.FailureReason ?? "We could not record this payment.") +
                " Please contact the store with reference " +
                intent.Reference + ".",
                permanent: true);
    }
}