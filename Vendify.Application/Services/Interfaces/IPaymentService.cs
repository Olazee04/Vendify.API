using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Payment;

namespace Vendify.Application.Services.Interfaces
{
    public interface IPaymentService
    {
        // Paystack
        Task<ApiResponse<PaymentInitiateDto>> InitiatePaystackPaymentAsync(
            Guid orderId, Guid storeId, string? callbackUrl);

        Task<ApiResponse<PaymentVerifyDto>> VerifyPaystackPaymentAsync(
            string reference, Guid storeId);

        // Flutterwave
        Task<ApiResponse<PaymentInitiateDto>> InitiateFlutterwavePaymentAsync(
            Guid orderId, Guid storeId, string? callbackUrl);

        Task<ApiResponse<PaymentVerifyDto>> VerifyFlutterwavePaymentAsync(
            string reference, Guid storeId);

        // Stripe
        Task<ApiResponse<StripePaymentDto>> InitiateStripePaymentAsync(
            Guid orderId, Guid storeId, string currency);

        Task<ApiResponse<PaymentVerifyDto>> VerifyStripePaymentAsync(
            string paymentIntentId, Guid storeId);

        // Unified — auto-routes based on provider string
        Task<ApiResponse<PaymentInitiateDto>> InitiatePaymentAsync(
            InitiatePaymentRequest request, Guid storeId);

        Task<ApiResponse<PaymentVerifyDto>> VerifyPaymentAsync(
            VerifyPaymentRequest request, Guid storeId);
    }
}