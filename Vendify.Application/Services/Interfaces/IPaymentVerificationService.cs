using Vendify.Core.Enums;

namespace Vendify.Application.Services.Interfaces
{
    public record PaymentVerificationResult(bool IsVerified, string Message);

    public interface IPaymentVerificationService
    {
        Task<PaymentVerificationResult> VerifyAsync(
            PaymentMethod method,
            string reference,
            decimal expectedAmountNaira);
    }
}