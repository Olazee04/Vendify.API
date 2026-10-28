using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Payment;

namespace Vendify.Application.Services.Interfaces
{
    public interface IPaymentFulfilmentService
    {
        // Saves the order details BEFORE the customer pays
        Task<ApiResponse<PaymentIntentDto>> CreateIntentAsync(
            string storeSlug, CreatePaymentIntentRequest request);

        // Records the order / deposit / checkout once payment is made.
        // Safe to call many times, only the first call does the work.
        Task<FulfilResult> FulfilAsync(string reference);
    }
}