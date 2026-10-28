using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Payments;

namespace Vendify.Application.Services.Interfaces
{
    public interface IPaymentAttentionService
    {
        Task<ApiResponse<PaymentAttentionListDto>> ListAsync(
            Guid storeId, bool resolved);

        Task<ApiResponse<bool>> RetryAsync(Guid storeId, Guid intentId);

        Task<ApiResponse<bool>> ResolveAsync(
            Guid storeId, Guid intentId, string? note);
    }
}