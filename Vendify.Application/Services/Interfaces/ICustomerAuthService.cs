using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Customers;

namespace Vendify.Application.Services.Interfaces
{
    public interface ICustomerAuthService
    {
        Task<ApiResponse<bool>> RegisterAsync(
            string storeSlug, CustomerRegisterRequest request);

        Task<ApiResponse<CustomerSessionDto>> VerifyEmailAsync(
            string storeSlug, CustomerVerifyRequest request);

        Task<ApiResponse<bool>> ResendVerificationAsync(
            string storeSlug, string? email);

        Task<ApiResponse<CustomerSessionDto>> LoginAsync(
            string storeSlug, CustomerLoginRequest request);

        Task<ApiResponse<bool>> ForgotPasswordAsync(
            string storeSlug, string? email);

        Task<ApiResponse<bool>> ResetPasswordAsync(
            string storeSlug, CustomerResetRequest request);

        Task<ApiResponse<CustomerSessionDto>> ChangePasswordAsync(
            string storeSlug, CustomerChangePasswordRequest request);

        Task<ApiResponse<CustomerProfileDto>> GetProfileAsync(
            string storeSlug, string? token);

        Task<ApiResponse<List<CustomerOrderSummaryDto>>> GetOrdersAsync(
            string storeSlug, string? token);

        // Used by the stockpile code to know who is signed in
        Task<CustomerIdentity?> ResolveAsync(string storeSlug, string? token);
    }
}