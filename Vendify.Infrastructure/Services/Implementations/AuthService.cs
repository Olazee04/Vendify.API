using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Auth;
using Vendify.Application.DTOs.Notification;
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

public async Task<ApiResponse<RegisterResponse>> RegisterAsync(
    RegisterRequest request)
{
    var existingUser = await _context.Users
        .FirstOrDefaultAsync(u =>
            u.Email == request.Email.ToLower());

    if (existingUser != null)
        return ApiResponse<RegisterResponse>
            .FailureResponse("Email is already registered");

    // Validate email format
    var emailRegex = new System.Text.RegularExpressions
        .Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");

    if (!emailRegex.IsMatch(request.Email))
        return ApiResponse<RegisterResponse>
            .FailureResponse("Please enter a valid email address");

    var blockedDomains = new[]
    {
        "mailinator.com", "guerrillamail.com", "throwaway.com",
        "yopmail.com", "trashmail.com", "tempmail.com",
        "temp-mail.org", "sharklasers.com", "maildrop.cc",
        "dispostable.com", "fakeinbox.com", "spam4.me",
        "mailnull.com", "mytrashmail.com", "example.com",
        "test.com", "fake.com"
    };

    var emailDomain = request.Email.Split('@')
        .LastOrDefault()?.ToLower();

    if (blockedDomains.Contains(emailDomain))
        return ApiResponse<RegisterResponse>
            .FailureResponse(
                "Disposable emails are not allowed. " +
                "Please use your real email address.");

    // Generate OTP
    var otp = new Random().Next(100000, 999999).ToString();

    var user = new User
    {
        FirstName = request.FirstName.Trim(),
        LastName = request.LastName.Trim(),
        Email = request.Email.ToLower().Trim(),
        PasswordHash = BCrypt.Net.BCrypt
            .HashPassword(request.Password),
        PhoneNumber = request.PhoneNumber,
        Role = UserRole.Merchant,
        IsVerified = false,
        VerificationToken = Guid.NewGuid().ToString("N"),
        OtpCode = otp,
        OtpExpiresAt = DateTime.UtcNow.AddMinutes(10),
    };

    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Send OTP email (fire and forget)
    _ = _notificationService.SendEmailAsync(new SendEmailRequest
    {
        ToEmail = user.Email,
        ToName = user.FirstName,
        Subject = "Verify your Vendify account",
        Body = $@"
            <div style='font-family:Arial,sans-serif;max-width:500px;
                margin:0 auto;padding:20px'>
                <div style='background:#7c3aed;padding:20px;
                    border-radius:12px 12px 0 0;text-align:center'>
                    <h1 style='color:white;margin:0;font-size:24px'>
                        Vendify
                    </h1>
                </div>
                <div style='background:#f9fafb;padding:30px;
                    border-radius:0 0 12px 12px'>
                    <p style='color:#374151'>
                        Hi <strong>{user.FirstName}</strong>,
                    </p>
                    <p style='color:#374151'>
                        Enter this code to verify your Vendify account:
                    </p>
                    <div style='text-align:center;margin:30px 0'>
                        <div style='background:white;
                            border:2px solid #7c3aed;
                            border-radius:12px;padding:20px;
                            display:inline-block'>
                            <span style='font-size:36px;font-weight:900;
                                color:#7c3aed;letter-spacing:12px'>
                                {otp}
                            </span>
                        </div>
                    </div>
                    <p style='color:#9ca3af;font-size:13px'>
                        This code expires in 10 minutes.
                    </p>
                    <p style='color:#9ca3af;font-size:13px'>
                        If you did not create a Vendify account,
                        ignore this email.
                    </p>
                </div>
            </div>",
        IsHtml = true,


    });

    return ApiResponse<RegisterResponse>.SuccessResponse(
        new RegisterResponse
        {
            User = MapToUserDto(user),
            Message =
                "Registration successful! " +
                "Check your email for the verification code.",
        },
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

        public async Task<ApiResponse<AuthResponse>> VerifyOtpAsync(
    VerifyOtpRequest request)
        {
            User? user = null;

            if (!string.IsNullOrEmpty(request.Email))
                user = await _context.Users
                    .Include(u => u.Store)
                    .FirstOrDefaultAsync(u =>
                        u.Email == request.Email.ToLower());

            if (!string.IsNullOrEmpty(request.PhoneNumber)
                && user == null)
                user = await _context.Users
                    .Include(u => u.Store)
                    .FirstOrDefaultAsync(u =>
                        u.PhoneNumber == request.PhoneNumber);

            if (user == null)
                return ApiResponse<AuthResponse>
                    .FailureResponse("User not found");

            if (user.IsVerified)
            {
                // Already verified — just log them in
                var existingToken =
                    _jwtHelper.GenerateAccessToken(user);
                var existingRefresh =
                    _jwtHelper.GenerateRefreshToken();
                user.RefreshToken = existingRefresh;
                user.RefreshTokenExpiry =
                    DateTime.UtcNow.AddDays(7);
                await _context.SaveChangesAsync();
                return ApiResponse<AuthResponse>.SuccessResponse(
                    BuildAuthResponse(user, existingToken,
                        existingRefresh),
                    "Account already verified. Logged in!");
            }

            if (user.OtpCode != request.Otp)
                return ApiResponse<AuthResponse>
                    .FailureResponse("Invalid verification code");

            if (user.OtpExpiresAt < DateTime.UtcNow)
                return ApiResponse<AuthResponse>
                    .FailureResponse(
                        "Code has expired. Please request a new one.");

            // Mark verified and clear OTP
            user.IsVerified = true;
            user.OtpCode = null;
            user.OtpExpiresAt = null;

            // Generate tokens for auto-login
            var accessToken = _jwtHelper.GenerateAccessToken(user);
            var refreshToken = _jwtHelper.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();

            // Send welcome email
            _ = _notificationService.SendWelcomeEmailAsync(
                user.Email, user.FirstName);

            return ApiResponse<AuthResponse>.SuccessResponse(
                BuildAuthResponse(user, accessToken, refreshToken),
                "Email verified! Welcome to Vendify!");
        }

        public async Task<ApiResponse> ResendOtpAsync(
            ResendOtpRequest request)
        {
            User? user = null;

            if (!string.IsNullOrEmpty(request.Email))
                user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email == request.Email.ToLower());

            if (user == null)
                return ApiResponse.FailureResponse("User not found");

            if (user.IsVerified)
                return ApiResponse.FailureResponse(
                    "Account is already verified");

            var otp = new Random()
                .Next(100000, 999999).ToString();
            user.OtpCode = otp;
            user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(10);
            await _context.SaveChangesAsync();

            _ = _notificationService.SendEmailAsync(
                new SendEmailRequest
                {
                    ToEmail = user.Email,
                    ToName = user.FirstName,
                    Subject = "Your new Vendify verification code",
                    Body = $@"
                <div style='font-family:Arial,sans-serif;
                    max-width:500px;margin:0 auto;padding:20px'>
                    <div style='background:#7c3aed;padding:20px;
                        border-radius:12px 12px 0 0;
                        text-align:center'>
                        <h1 style='color:white;margin:0;
                            font-size:24px'>Vendify</h1>
                    </div>
                    <div style='background:#f9fafb;padding:30px;
                        border-radius:0 0 12px 12px'>
                        <p style='color:#374151'>
                            Hi <strong>{user.FirstName}</strong>,
                        </p>
                        <p style='color:#374151'>
                            Your new verification code is:
                        </p>
                        <div style='text-align:center;margin:30px 0'>
                            <div style='background:white;
                                border:2px solid #7c3aed;
                                border-radius:12px;padding:20px;
                                display:inline-block'>
                                <span style='font-size:36px;
                                    font-weight:900;color:#7c3aed;
                                    letter-spacing:12px'>
                                    {otp}
                                </span>
                            </div>
                        </div>
                        <p style='color:#9ca3af;font-size:13px'>
                            Expires in 10 minutes.
                        </p>
                    </div>
                </div>",
                    IsHtml = true,
                });

            return ApiResponse.SuccessResponse(
                "New verification code sent!");
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