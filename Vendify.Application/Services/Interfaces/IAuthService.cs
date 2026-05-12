using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Auth;

namespace Vendify.Application.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
        Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
        Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request);
        Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request);
        Task<ApiResponse> ChangePasswordAsync(ChangePasswordRequest request, Guid userId);
        Task<ApiResponse> VerifyEmailAsync(string token);
        Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId);
    }
}