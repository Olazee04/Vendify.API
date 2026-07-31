using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vendify.Application.DTOs.Notification;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Jobs;

public class OrderReminderJob
{
    private readonly VendifyDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<OrderReminderJob> _logger;

    public OrderReminderJob(
        VendifyDbContext db,
        INotificationService notifications,
        ILogger<OrderReminderJob> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task RemindPendingOrdersAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-2);

        var pendingOrders = await _db.Orders
            .Include(o => o.Store)
            .Where(o =>
                o.Status == OrderStatus.Pending &&
                o.CreatedAt <= cutoff &&
                o.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .ToListAsync();

        _logger.LogInformation(
            "Found {Count} pending orders to remind",
            pendingOrders.Count);

        foreach (var order in pendingOrders)
        {
            try
            {
                if (!string.IsNullOrEmpty(
                    order.Store?.WhatsAppNumber))
                {
                    await _notifications
                        .SendWhatsAppMessageAsync(
                            new WhatsAppMessageDto
                            {
                                PhoneNumber = order.Store.WhatsAppNumber,
                                Message =
                                    $"Reminder: Order " +
                                    $"{order.OrderNumber} " +
                                    $"has been pending for " +
                                    $"over 2 hours.\n" +
                                    $"Customer: " +
                                    $"{order.CustomerName}\n" +
                                    $"Amount: " +
                                    $"N{order.Total:N0}"
                            });
                }

                if (!string.IsNullOrEmpty(
                    order.Store?.SupportEmail))
                {
                    await _notifications.SendEmailAsync(
     new SendEmailRequest
     {
         ToEmail = order.Store.SupportEmail,
         ToName = order.Store.Name,
         Subject =
             $"Pending Order Reminder " +
             $"- {order.OrderNumber}",
         Body =
             $"Order {order.OrderNumber}" +
             $" from {order.CustomerName}" +
             $" is still pending.\n" +
             $"Amount: N{order.Total:N0}"
     });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to remind for order {OrderId}",
                    order.Id);
            }
        }
    }

    public async Task LowStockAlertsAsync()
    {
        var stores = await _db.Stores
            .Include(s => s.Products)
            .Where(s =>
                s.Status == StoreStatus.Active)
            .ToListAsync();

        foreach (var store in stores)
        {
            var lowStock = store.Products
                .Where(p =>
                    p.TrackInventory &&
                    p.StockQuantity > 0 &&
                    p.StockQuantity <= 5)
                .ToList();

            var outOfStock = store.Products
                .Where(p =>
                    p.TrackInventory &&
                    p.StockQuantity == 0)
                .ToList();

            if (!lowStock.Any() && !outOfStock.Any())
                continue;

            _logger.LogWarning(
                "Store {Store}: {OutCount} out of stock, " +
                "{LowCount} low stock",
                store.Name,
                outOfStock.Count,
                lowStock.Count);

            if (!string.IsNullOrEmpty(store.WhatsAppNumber))
            {
                var outList = outOfStock.Any()
                    ? "Out of stock:\n" + string.Join("\n",
                        outOfStock.Select(p => $"- {p.Name}"))
                    : "";

                var lowList = lowStock.Any()
                    ? "Low stock:\n" + string.Join("\n",
                        lowStock.Select(p =>
                            $"- {p.Name}: " +
                            $"{p.StockQuantity} left"))
                    : "";

                await _notifications
                    .SendWhatsAppMessageAsync(
                        new WhatsAppMessageDto
                        {
                            PhoneNumber = store.WhatsAppNumber,
                            Message =
                                $"Daily Inventory Alert " +
                                $"for {store.Name}\n\n" +
                                $"{outList}\n{lowList}"
                        });
            }
        }
    }

    public async Task ProcessPendingWebhooksAsync()
    {
        _logger.LogInformation(
            "Processing pending webhooks...");
        await Task.CompletedTask;
    }

    public async Task WeeklyReportAsync()
    {
        var weekStart = DateTime.UtcNow.AddDays(-7);

        var stores = await _db.Stores
            .Include(s => s.Orders)
            .Where(s =>
                s.Status == StoreStatus.Active)
            .ToListAsync();

        foreach (var store in stores)
        {
            var weekOrders = store.Orders
                .Where(o => o.CreatedAt >= weekStart)
                .ToList();

            if (!weekOrders.Any()) continue;

            var revenue = weekOrders
                .Where(o =>
                    o.PaymentStatus == PaymentStatus.Paid)
                .Sum(o => o.Total);

            var pending = weekOrders
                .Count(o =>
                    o.Status == OrderStatus.Pending);

            _logger.LogInformation(
                "Weekly: {Store} — {Orders} orders, " +
                "N{Revenue}, {Pending} pending",
                store.Name,
                weekOrders.Count,
                revenue,
                pending);

            if (!string.IsNullOrEmpty(store.WhatsAppNumber))
            {
                await _notifications
                    .SendWhatsAppMessageAsync(
                        new WhatsAppMessageDto
                        {
                            PhoneNumber = store.WhatsAppNumber,
                            Message =
                                $"Weekly Report for " +
                                $"{store.Name}\n\n" +
                                $"Orders: {weekOrders.Count}\n" +
                                $"Revenue: N{revenue:N0}\n" +
                                $"Pending: {pending}"
                        });
            }
        }
    }
}