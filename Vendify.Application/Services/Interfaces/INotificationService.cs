using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Notification;

namespace Vendify.Application.Services.Interfaces
{
    public interface INotificationService
    {
        // Email
        Task<bool> SendEmailAsync(SendEmailRequest request);
        Task<bool> SendOrderConfirmationAsync(OrderEmailDto order);
        Task<bool> SendOrderStatusUpdateAsync(OrderEmailDto order);
        Task<bool> SendPasswordResetEmailAsync(
            string email, string name, string resetToken);
        Task<bool> SendWelcomeEmailAsync(string email, string name);

        // WhatsApp
        Task<bool> SendWhatsAppMessageAsync(WhatsAppMessageDto message);
        Task<bool> SendOrderWhatsAppNotificationAsync(
            OrderEmailDto order, string merchantWhatsApp);
    }
}