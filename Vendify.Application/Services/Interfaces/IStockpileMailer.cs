namespace Vendify.Application.Services.Interfaces
{
    public interface IStockpileMailer
    {
        Task<bool> SendAccessCodeAsync(
            string toEmail, string storeName, string code);
    }
}