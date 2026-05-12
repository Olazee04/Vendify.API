using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Coupon;

namespace Vendify.Application.Services.Interfaces
{
    public interface ICouponService
    {
        Task<ApiResponse<CouponDto>> CreateCouponAsync(
            CreateCouponRequest request, Guid storeId);

        Task<ApiResponse<List<CouponDto>>> GetStoreCouponsAsync(
            Guid storeId);

        Task<ApiResponse<CouponDto>> GetCouponByIdAsync(
            Guid couponId, Guid storeId);

        Task<ApiResponse<CouponDto>> UpdateCouponAsync(
            Guid couponId, UpdateCouponRequest request, Guid storeId);

        Task<ApiResponse<CouponValidateDto>> ValidateCouponAsync(
            ValidateCouponRequest request);

        Task<ApiResponse> DeleteCouponAsync(
            Guid couponId, Guid storeId);

        Task<ApiResponse> ToggleCouponAsync(
            Guid couponId, Guid storeId);
    }
}