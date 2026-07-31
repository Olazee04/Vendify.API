namespace Vendify.Application.Services.Interfaces;

public interface IWhatsAppBotService
{
    Task ProcessMessageAsync(
        string storeSlug,
        object payload);
    Task SendCatalogAsync(
        string phone,
        string storeSlug);
    Task SendProductDetailsAsync(
        string phone,
        string productId);
    Task SendOrderConfirmationAsync(
        string phone,
        string orderSummary);
}