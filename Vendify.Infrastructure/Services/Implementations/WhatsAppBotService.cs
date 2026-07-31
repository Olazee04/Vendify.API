using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vendify.Application.Services.Interfaces;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations;

public class WhatsAppBotService : IWhatsAppBotService
{
    private readonly VendifyDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppBotService> _logger;

    // Session state per phone number
    private static readonly Dictionary<string,
        UserSession> _sessions = new();

    public WhatsAppBotService(
        VendifyDbContext db,
        IHttpClientFactory clientFactory,
        IConfiguration config,
        ILogger<WhatsAppBotService> logger)
    {
        _db = db;
        _httpClient = clientFactory.CreateClient();
        _config = config;
        _logger = logger;
    }

    public async Task ProcessMessageAsync(
        string storeSlug,
        object payload)
    {
        try
        {
            var json = JsonSerializer
                .Serialize(payload);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract message data
            var entry = root
                .GetProperty("entry")[0];
            var changes = entry
                .GetProperty("changes")[0];
            var value = changes.GetProperty("value");

            if (!value.TryGetProperty(
                "messages", out var messages))
                return;

            var message = messages[0];
            var from = message
                .GetProperty("from").GetString() ?? "";
            var msgType = message
                .GetProperty("type").GetString();

            string text = "";
            if (msgType == "text")
            {
                text = message
                    .GetProperty("text")
                    .GetProperty("body")
                    .GetString() ?? "";
            }

            _logger.LogInformation(
                $"WhatsApp message from {from}: {text}");

            await HandleMessage(
                from, text.ToLower().Trim(), storeSlug);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Failed to process WhatsApp message");
        }
    }

    private async Task HandleMessage(
        string phone,
        string text,
        string storeSlug)
    {
        // Get or create session
        if (!_sessions.ContainsKey(phone))
            _sessions[phone] = new UserSession();

        var session = _sessions[phone];

        // Load store
        var store = await _db.Stores
    .Include(s => s.Products)
    .FirstOrDefaultAsync(s =>
        s.Slug == storeSlug &&
        s.Status == Core.Enums.StoreStatus.Active);

        if (store == null) return;

        // Handle based on text
        if (IsGreeting(text) ||
            text == "" ||
            text == "hi" ||
            text == "hello" ||
            text == "start")
        {
            session.State = "menu";
            await SendWelcomeMessage(
                phone, store.Name);
            return;
        }

        if (text == "1" ||
            text.Contains("catalog") ||
            text.Contains("products") ||
            text.Contains("shop"))
        {
            await SendCatalogAsync(phone, storeSlug);
            return;
        }

        if (text == "2" ||
            text.Contains("order") ||
            text.Contains("my order") ||
            text.Contains("track"))
        {
            session.State = "track_order";
            await SendTextMessage(phone,
                "📦 Please send your order number " +
                "(e.g. VND-20260101-0001)");
            return;
        }

        if (text == "3" ||
            text.Contains("contact") ||
            text.Contains("help") ||
            text.Contains("support"))
        {
            await SendTextMessage(phone,
                $"📞 *{store.Name} Support*\n\n" +
                $"WhatsApp: {store.WhatsAppNumber}\n" +
                $"Email: {store.SupportEmail ?? "N/A"}\n\n" +
                "Reply *0* to go back to main menu");
            return;
        }

        // Handle order number lookup
        if (session.State == "track_order")
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o =>
                    o.OrderNumber.ToLower() ==
                    text.ToUpper() &&
                    o.StoreId == store.Id);

            if (order != null)
            {
                session.State = "menu";
                await SendTextMessage(phone,
                    $"📦 *Order {order.OrderNumber}*\n\n" +
                    $"Status: *{order.Status}*\n" +
                    $"Payment: *{order.PaymentStatus}*\n" +
                    $"Total: *₦{order.Total.ToString("N0")}*\n" +
                    $"Items: {order.Items.Count} item(s)\n" +
                    (order.TrackingNumber != null
                        ? $"Tracking: *{order.TrackingNumber}*\n"
                        : "") +
                    "\nReply *0* for main menu");
            }
            else
            {
                await SendTextMessage(phone,
                    "❌ Order not found. " +
                    "Please check the order number.\n\n" +
                    "Reply *0* for main menu");
            }
            return;
        }

        // Check if they typed a product number
        if (session.State == "catalog" &&
            int.TryParse(text, out int productNum))
        {
            var products = await _db.Products
                .Where(p =>
                    p.StoreId == store.Id &&
                    p.IsPublished)
                .OrderBy(p => p.Name)
                .ToListAsync();

            if (productNum > 0 &&
                productNum <= products.Count)
            {
                var product = products[productNum - 1];
                session.State = "product_detail";
                session.SelectedProductId = product.Id.ToString();

                await SendTextMessage(phone,
                    $"🛍️ *{product.Name}*\n\n" +
                    (product.Description != null
                        ? $"{product.Description}\n\n"
                        : "") +
                    $"💰 Price: *₦{product.Price:N0}*\n" +
                    (product.CompareAtPrice.HasValue
                        ? $"~~₦{product.CompareAtPrice:N0}~~\n"
                        : "") +
                    $"📦 Stock: " +
                    (product.StockQuantity > 0
                        ? $"{product.StockQuantity} available"
                        : "❌ Out of stock") +
                    "\n\n" +
                    "Reply *order* to order this item\n" +
                    "Reply *back* to see catalog\n" +
                    "Reply *0* for main menu");
                return;
            }
        }

        // Handle order intent from product
        if (session.State == "product_detail" &&
            text == "order")
        {
            session.State = "collecting_name";
            await SendTextMessage(phone,
                "Great! Let's place your order 🎉\n\n" +
                "Please send your *full name*:");
            return;
        }

        // Collect order details
        if (session.State == "collecting_name")
        {
            session.CustomerName = text;
            session.State = "collecting_address";
            await SendTextMessage(phone,
                $"Thanks {session.CustomerName}! 👋\n\n" +
                "Please send your *delivery address*\n" +
                "(Street, City, State):");
            return;
        }

        if (session.State == "collecting_address")
        {
            session.DeliveryAddress = text;
            session.State = "confirm_order";

            var product = await _db.Products
                .FindAsync(session.SelectedProductId);

            await SendTextMessage(phone,
                $"📋 *Order Summary*\n\n" +
                $"Product: *{product?.Name}*\n" +
                $"Price: *₦{product?.Price:N0}*\n" +
                $"Delivery to: *{session.DeliveryAddress}*\n" +
                $"Name: *{session.CustomerName}*\n\n" +
                "Reply *confirm* to place order\n" +
                "Reply *cancel* to cancel");
            return;
        }

        if (session.State == "confirm_order" &&
            text == "confirm")
        {
            // Create the order
            Guid.TryParse(
    session.SelectedProductId, out var prodGuid);
            var product = await _db.Products
                .FindAsync(prodGuid);

            if (product != null && product.StockQuantity > 0)
            {
                var addressParts = session.DeliveryAddress
                    .Split(',');
                var orderNum =
                    $"VND-{DateTime.Now:yyyyMMdd}-" +
                    $"{new Random().Next(1000, 9999)}";

                var order = new Core.Entities.Order
                {
                    StoreId = store.Id,
                    OrderNumber = orderNum,
                    CustomerName = session.CustomerName,
                    CustomerPhone = phone,
                    CustomerEmail =
                        $"{phone}@whatsapp.com",
                    Status = Core.Enums.OrderStatus.Pending,
                    PaymentStatus =
                        Core.Enums.PaymentStatus.Unpaid,
                    Subtotal = product.Price,
                    Total = product.Price,
                    ShippingAddress =
                        new Core.Entities.ShippingAddress
                        {
                            FullName = session.CustomerName,
                            PhoneNumber = phone,
                            AddressLine1 =
                                addressParts[0].Trim(),
                            City = addressParts.Length > 1
                                ? addressParts[1].Trim()
                                : "Unknown",
                            State = addressParts.Length > 2
                                ? addressParts[2].Trim()
                                : "Unknown",
                            Country = "Nigeria",
                        },
                    Items = new List<Core.Entities.OrderItem>
                    {
                        new()
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            Quantity = 1,
                            UnitPrice = product.Price,
                            TotalPrice = product.Price,
                        }
                    }
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                session.State = "menu";

                await SendTextMessage(phone,
                    $"✅ *Order Placed Successfully!*\n\n" +
                    $"Order Number: *{orderNum}*\n" +
                    $"Product: *{product.Name}*\n" +
                    $"Amount: *₦{product.Price:N0}*\n\n" +
                    $"Our team will contact you soon " +
                    $"to arrange payment and delivery.\n\n" +
                    $"Track your order by sending " +
                    $"your order number anytime.\n\n" +
                    $"Thank you for shopping at " +
                    $"*{store.Name}*! 🛍️");
            }
            else
            {
                session.State = "menu";
                await SendTextMessage(phone,
                    "❌ Sorry, this item is out of stock.\n\n" +
                    "Reply *1* to see other products.");
            }
            return;
        }

        if (text == "back")
        {
            await SendCatalogAsync(phone, storeSlug);
            return;
        }

        if (text == "0" || text == "menu")
        {
            session.State = "menu";
            await SendWelcomeMessage(phone, store.Name);
            return;
        }

        // Default — show menu again
        await SendWelcomeMessage(phone, store.Name);
    }

    public async Task SendCatalogAsync(
        string phone,
        string storeSlug)
    {
        var store = await _db.Stores
            .FirstOrDefaultAsync(s =>
                s.Slug == storeSlug);

        if (store == null) return;

        var products = await _db.Products
            .Where(p =>
                p.StoreId == store.Id &&
                p.IsPublished)
            .OrderBy(p => p.Name)
            .Take(20)
            .ToListAsync();

        if (!products.Any())
        {
            await SendTextMessage(phone,
                "📦 No products available yet.\n\n" +
                "Check back soon!");
            return;
        }

        if (!_sessions.ContainsKey(phone))
            _sessions[phone] = new UserSession();
        _sessions[phone].State = "catalog";

        var catalog = new StringBuilder();
        catalog.AppendLine($"🛍️ *{store.Name} — Product Catalog*");
        catalog.AppendLine();
        catalog.AppendLine(
            "Reply with a number to see details:\n");

        for (int i = 0; i < products.Count; i++)
        {
            var p = products[i];
            catalog.AppendLine(
                $"*{i + 1}.* {p.Name}\n" +
                $"    ₦{p.Price:N0}" +
                (p.StockQuantity == 0
                    ? " (Out of stock)" : "") +
                "\n");
        }

        catalog.AppendLine("Reply *0* for main menu");

        await SendTextMessage(phone, catalog.ToString());
    }

    public async Task SendProductDetailsAsync(
        string phone,
        string productId)
    {
        Guid.TryParse(productId, out var pid);
        var product = await _db.Products.FindAsync(pid);
        if (product == null) return;

        await SendTextMessage(phone,
            $"*{product.Name}*\n" +
            $"Price: ₦{product.Price:N0}\n" +
            $"{product.Description}");
    }

    public async Task SendOrderConfirmationAsync(
        string phone,
        string orderSummary)
    {
        await SendTextMessage(phone,
            $"✅ Order Confirmed!\n\n{orderSummary}");
    }

    private async Task SendWelcomeMessage(
        string phone,
        string storeName)
    {
        await SendTextMessage(phone,
            $"👋 Welcome to *{storeName}*!\n\n" +
            "How can we help you today?\n\n" +
            "Reply with a number:\n" +
            "*1* 🛍️ Browse Products\n" +
            "*2* 📦 Track My Order\n" +
            "*3* 📞 Contact Support\n\n" +
            "_Powered by Vendify_");
    }

    private async Task SendTextMessage(
        string phone,
        string message)
    {
        var token = _config["WhatsApp:AccessToken"];
        var phoneNumberId =
            _config["WhatsApp:PhoneNumberId"];

        if (string.IsNullOrEmpty(token) ||
            string.IsNullOrEmpty(phoneNumberId))
        {
            _logger.LogWarning(
                "WhatsApp not configured");
            return;
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = phone,
            type = "text",
            text = new { body = message }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(
            json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add(
            "Authorization", $"Bearer {token}");

        await _httpClient.PostAsync(
            $"https://graph.facebook.com/v18.0/" +
            $"{phoneNumberId}/messages",
            content);
    }

    private static bool IsGreeting(string text) =>
        new[] { "hi", "hello", "hey", "hii",
            "good morning", "good afternoon",
            "good evening", "start", "menu" }
        .Any(g => text.Contains(g));
}

// Session state per user
public class UserSession
{
    public string State { get; set; } = "menu";
    public string? SelectedProductId { get; set; }
    public string CustomerName { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
}