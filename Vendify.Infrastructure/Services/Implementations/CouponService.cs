using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Coupon;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class CouponService : ICouponService
    {
        private readonly VendifyDbContext _context;

        public CouponService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<CouponDto>> CreateCouponAsync(
            CreateCouponRequest request, Guid storeId)
        {
            // Force code to uppercase
            var code = request.Code.ToUpper().Trim();

            // Check for duplicate code in store
            var exists = await _context.Coupons
                .AnyAsync(c =>
                    c.StoreId == storeId && c.Code == code);

            if (exists)
                return ApiResponse<CouponDto>.FailureResponse(
                    "A coupon with this code already exists in your store");

            // Validate percentage discount
            if (request.DiscountType == DiscountType.Percentage &&
                request.DiscountValue > 100)
                return ApiResponse<CouponDto>.FailureResponse(
                    "Percentage discount cannot exceed 100%");

            var coupon = new Coupon
            {
                Code = code,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                MinimumOrderAmount = request.MinimumOrderAmount,
                UsageLimit = request.UsageLimit,
                ExpiresAt = request.ExpiresAt,
                IsActive = request.IsActive,
                UsageCount = 0,
                StoreId = storeId
            };

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            return ApiResponse<CouponDto>.SuccessResponse(
                MapToDto(coupon), "Coupon created successfully");
        }

        public async Task<ApiResponse<List<CouponDto>>>
            GetStoreCouponsAsync(Guid storeId)
        {
            var coupons = await _context.Coupons
                .Where(c => c.StoreId == storeId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return ApiResponse<List<CouponDto>>.SuccessResponse(
                coupons.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<CouponDto>> GetCouponByIdAsync(
            Guid couponId, Guid storeId)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.Id == couponId && c.StoreId == storeId);

            if (coupon == null)
                return ApiResponse<CouponDto>.FailureResponse(
                    "Coupon not found");

            return ApiResponse<CouponDto>.SuccessResponse(MapToDto(coupon));
        }

        public async Task<ApiResponse<CouponDto>> UpdateCouponAsync(
            Guid couponId, UpdateCouponRequest request, Guid storeId)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.Id == couponId && c.StoreId == storeId);

            if (coupon == null)
                return ApiResponse<CouponDto>.FailureResponse(
                    "Coupon not found");

            if (request.DiscountValue.HasValue)
            {
                if (coupon.DiscountType == DiscountType.Percentage &&
                    request.DiscountValue > 100)
                    return ApiResponse<CouponDto>.FailureResponse(
                        "Percentage discount cannot exceed 100%");

                coupon.DiscountValue = request.DiscountValue.Value;
            }

            if (request.MinimumOrderAmount.HasValue)
                coupon.MinimumOrderAmount = request.MinimumOrderAmount;
            if (request.UsageLimit.HasValue)
                coupon.UsageLimit = request.UsageLimit;
            if (request.ExpiresAt.HasValue)
                coupon.ExpiresAt = request.ExpiresAt;
            if (request.IsActive.HasValue)
                coupon.IsActive = request.IsActive.Value;

            coupon.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<CouponDto>.SuccessResponse(
                MapToDto(coupon), "Coupon updated successfully");
        }

        public async Task<ApiResponse<CouponValidateDto>> ValidateCouponAsync(
            ValidateCouponRequest request)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s =>
                    s.Slug == request.StoreSlug.ToLower());

            if (store == null)
                return ApiResponse<CouponValidateDto>.SuccessResponse(
                    new CouponValidateDto
                    {
                        IsValid = false,
                        Message = "Store not found"
                    });

            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.Code == request.Code.ToUpper() &&
                    c.StoreId == store.Id);

            if (coupon == null)
                return ApiResponse<CouponValidateDto>.SuccessResponse(
                    new CouponValidateDto
                    {
                        IsValid = false,
                        Message = "Invalid coupon code"
                    });

            // Check if active
            if (!coupon.IsActive)
                return ApiResponse<CouponValidateDto>.SuccessResponse(
                    new CouponValidateDto
                    {
                        IsValid = false,
                        Message = "This coupon is no longer active"
                    });

            // Check expiry
            if (coupon.ExpiresAt.HasValue &&
                coupon.ExpiresAt < DateTime.UtcNow)
                return ApiResponse<CouponValidateDto>.SuccessResponse(
                    new CouponValidateDto
                    {
                        IsValid = false,
                        Message = "This coupon has expired"
                    });

            // Check usage limit
            if (coupon.UsageLimit.HasValue &&
                coupon.UsageCount >= coupon.UsageLimit)
                return ApiResponse<CouponValidateDto>.SuccessResponse(
                    new CouponValidateDto
                    {
                        IsValid = false,
                        Message = "This coupon has reached its usage limit"
                    });

            // Check minimum order amount
            if (coupon.MinimumOrderAmount.HasValue &&
                request.OrderAmount < coupon.MinimumOrderAmount)
                return ApiResponse<CouponValidateDto>.SuccessResponse(
                    new CouponValidateDto
                    {
                        IsValid = false,
                        Message = $"Minimum order amount is " +
                            $"{coupon.MinimumOrderAmount:N2} to use this coupon"
                    });

            // Calculate discount
            var discountAmount = coupon.DiscountType == DiscountType.Percentage
                ? request.OrderAmount * (coupon.DiscountValue / 100)
                : coupon.DiscountValue;

            // Cap discount at order amount
            discountAmount = Math.Min(discountAmount, request.OrderAmount);
            var finalAmount = request.OrderAmount - discountAmount;

            return ApiResponse<CouponValidateDto>.SuccessResponse(
                new CouponValidateDto
                {
                    IsValid = true,
                    Message = "Coupon applied successfully",
                    Code = coupon.Code,
                    DiscountType = coupon.DiscountType.ToString(),
                    DiscountValue = coupon.DiscountValue,
                    DiscountAmount = discountAmount,
                    OriginalAmount = request.OrderAmount,
                    FinalAmount = finalAmount
                });
        }

        public async Task<ApiResponse> DeleteCouponAsync(
            Guid couponId, Guid storeId)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.Id == couponId && c.StoreId == storeId);

            if (coupon == null)
                return ApiResponse.FailureResponse("Coupon not found");

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse(
                "Coupon deleted successfully");
        }

        public async Task<ApiResponse> ToggleCouponAsync(
            Guid couponId, Guid storeId)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.Id == couponId && c.StoreId == storeId);

            if (coupon == null)
                return ApiResponse.FailureResponse("Coupon not found");

            coupon.IsActive = !coupon.IsActive;
            coupon.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var status = coupon.IsActive ? "activated" : "deactivated";
            return ApiResponse.SuccessResponse(
                $"Coupon {status} successfully");
        }

        // ── Private Helpers ──────────────────────────────────

        private static CouponDto MapToDto(Coupon coupon)
        {
            var isExpired = coupon.ExpiresAt.HasValue &&
                coupon.ExpiresAt < DateTime.UtcNow;

            return new CouponDto
            {
                Id = coupon.Id,
                Code = coupon.Code,
                DiscountType = coupon.DiscountType.ToString(),
                DiscountValue = coupon.DiscountValue,
                MinimumOrderAmount = coupon.MinimumOrderAmount,
                UsageLimit = coupon.UsageLimit,
                UsageCount = coupon.UsageCount,
                RemainingUses = coupon.UsageLimit.HasValue
                    ? coupon.UsageLimit - coupon.UsageCount
                    : null,
                ExpiresAt = coupon.ExpiresAt,
                IsActive = coupon.IsActive,
                IsExpired = isExpired,
                StoreId = coupon.StoreId,
                CreatedAt = coupon.CreatedAt
            };
        }
    }
}