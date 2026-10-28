namespace Vendify.Application.Services.Interfaces
{
    public interface ICustomerMailer
    {
        // purpose is "Verify" or "Reset".
        // Returns null when the email was sent, otherwise a short reason.
        Task<string?> SendCodeAsync(
            string toEmail, string storeName, string purpose, string code);
    }
}