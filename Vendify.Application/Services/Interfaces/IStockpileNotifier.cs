namespace Vendify.Application.Services.Interfaces
{
    public interface IStockpileNotifier
    {
        void NotifyDeposit(
            Guid storeId, string customerName, string customerEmail,
            decimal amount, int itemCount, bool isFull);

        void NotifyCheckout(
            Guid storeId, string orderNumber,
            string customerName, decimal orderTotal);

        void NotifyPaymentProblem(
            Guid storeId, string reference, string kind, string reason);
    }
}