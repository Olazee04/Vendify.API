using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Auth;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;
using Vendify.Infrastructure.Helpers;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly VendifyDbContext _context;
        private readonly JwtHelper _jwtHelper;
        private readonly IConfiguration _configuration;
        private readonly INotificationService _notificationService;

        public AuthService(
            VendifyDbContext context,
            JwtHelper jwtHelper,
            IConfiguration configuration,
            INotificationService notificationService)
        {
            _context = context;
            _jwtHelper = jwtHelper;
            _configuration = configuration;
            _notificationService = notificationService;
        }

        public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

            if (existingUser != null)
                return ApiResponse<AuthResponse>.FailureResponse("Email is already registered");

            var user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.ToLower().Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                PhoneNumber = request.PhoneNumber,
                // Use provided role or default to Merchant
                Role = request.Role?.ToLower() == "customer"
         ? UserRole.Customer
         : UserRole.Merchant,
                IsVerified = false,
                VerificationToken = Guid.NewGuid().ToString("N")
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var accessToken = _jwtHelper.GenerateAccessToken(user);
            var refreshToken = _jwtHelper.GenerateRefreshToken();
            var refreshExpiryDays = int.Parse(
                _configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7");

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshExpiryDays);
            await _context.SaveChangesAsync();

            return ApiResponse<AuthResponse>.SuccessResponse(
                BuildAuthResponse(user, accessToken, refreshToken),
                "Registration successful");
        }

        public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .Include(u => u.Store)
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return ApiResponse<AuthResponse>.FailureResponse("Invalid email or password");

            if (user.IsDeleted)
                return ApiResponse<AuthResponse>.FailureResponse("Account has been deactivated");

            var accessToken = _jwtHelper.GenerateAccessToken(user);
            var refreshToken = _jwtHelper.GenerateRefreshToken();
            var refreshExpiryDays = int.Parse(
                _configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7");

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshExpiryDays);
            await _context.SaveChangesAsync();

            return ApiResponse<AuthResponse>.SuccessResponse(
                BuildAuthResponse(user, accessToken, refreshToken),
                "Login successful");
        }

        public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var user = await _context.Users
                .Include(u => u.Store)
                .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

            if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
                return ApiResponse<AuthResponse>.FailureResponse(
                    "Invalid or expired refresh token");

            var accessToken = _jwtHelper.GenerateAccessToken(user);
            var refreshToken = _jwtHelper.GenerateRefreshToken();
            var refreshExpiryDays = int.Parse(
                _configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7");

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshExpiryDays);
            await _context.SaveChangesAsync();

            return ApiResponse<AuthResponse>.SuccessResponse(
                BuildAuthResponse(user, accessToken, refreshToken),
                "Token refreshed");
        }

        public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

            if (user == null)
                return ApiResponse.SuccessResponse(
                    "If this email exists, a reset link has been sent");

            user.PasswordResetToken = Guid.NewGuid().ToString("N");
            user.PasswordResetExpiry = DateTime.UtcNow.AddHours(2);
            await _context.SaveChangesAsync();

            // TODO: Send email — Phase 11
            // Send welcome email (fire and forget)
            _ = _notificationService.SendPasswordResetEmailAsync(
     user.Email,
     user.FirstName,
     user.PasswordResetToken!);

            // Fire and forget — don't block registration
            _ = _notificationService.SendWelcomeEmailAsync(
                user.Email, user.FirstName);
            return ApiResponse.SuccessResponse(
                "If this email exists, a reset link has been sent");
        }

        public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == request.Email.ToLower() &&
                    u.PasswordResetToken == request.Token &&
                    u.PasswordResetExpiry > DateTime.UtcNow);

            if (user == null)
                return ApiResponse.FailureResponse("Invalid or expired reset token");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetExpiry = null;
            user.RefreshToken = null;
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse("Password reset successful");
        }

        public async Task<ApiResponse> ChangePasswordAsync(
            ChangePasswordRequest request, Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return ApiResponse.FailureResponse("User not found");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return ApiResponse.FailureResponse("Current password is incorrect");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.RefreshToken = null;
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse("Password changed successfully");
        }

        public async Task<ApiResponse> VerifyEmailAsync(string token)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.VerificationToken == token);

            if (user == null)
                return ApiResponse.FailureResponse("Invalid verification token");

            user.IsVerified = true;
            user.VerificationToken = null;
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse("Email verified successfully");
        }

        public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.Store)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return ApiResponse<UserDto>.FailureResponse("User not found");

            return ApiResponse<UserDto>.SuccessResponse(MapToUserDto(user));
        }

        // ── Private Helpers ──────────────────────────────────

        private AuthResponse BuildAuthResponse(
            User user, string accessToken, string refreshToken)
        {
            var expiryMinutes = int.Parse(
                _configuration["JwtSettings:ExpiryMinutes"] ?? "60");

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                User = MapToUserDto(user)
            };
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role.ToString(),
                IsVerified = user.IsVerified,
                HasStore = user.Store != null,
                StoreId = user.Store?.Id
            };
        }
    }
}