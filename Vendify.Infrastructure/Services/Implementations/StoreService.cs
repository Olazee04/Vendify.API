using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Store;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class StoreService : IStoreService
    {
        private readonly VendifyDbContext _context;

        public StoreService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<StoreDto>> CreateStoreAsync(
     CreateStoreRequest request, Guid userId)
        {
            // At least one social media required
            if (string.IsNullOrWhiteSpace(request.WhatsAppNumber) &&
                string.IsNullOrWhiteSpace(request.InstagramHandle) &&
                string.IsNullOrWhiteSpace(request.FacebookPageUrl) &&
                string.IsNullOrWhiteSpace(request.TwitterHandle))
            {
                return ApiResponse<StoreDto>.FailureResponse(
                    "Please provide at least one social media or contact link " +
                    "(WhatsApp, Instagram, Facebook or Twitter)");
            }

            // Check if user already has a store
            var existingStore = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (existingStore != null)
                return ApiResponse<StoreDto>.FailureResponse(
                    "You already have a store");

            var store = new Store
            {
                Name = request.Name.Trim(),
                Slug = request.Slug.ToLower().Trim(),
                Description = request.Description,
                WhatsAppNumber = request.WhatsAppNumber,
                InstagramHandle = request.InstagramHandle,
                FacebookPageUrl = request.FacebookPageUrl,
                TwitterHandle = request.TwitterHandle,
                SupportEmail = request.SupportEmail,
                Currency = request.Currency,
                Status = StoreStatus.Active,
                UserId = userId
            };

            _context.Stores.Add(store);
            await _context.SaveChangesAsync();

            return ApiResponse<StoreDto>.SuccessResponse(
                await MapToStoreDtoAsync(store),
                "Store created successfully");
        }

        public async Task<ApiResponse<StoreDto>> GetMyStoreAsync(Guid userId)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (store == null)
                return ApiResponse<StoreDto>.FailureResponse(
                    "You don't have a store yet");

            return ApiResponse<StoreDto>.SuccessResponse(
                await MapToStoreDtoAsync(store));
        }

        public async Task<ApiResponse<StorePublicDto>> GetStoreBySlugAsync(
            string slug)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s =>
                    s.Slug == slug.ToLower() &&
                    s.Status == StoreStatus.Active);

            if (store == null)
                return ApiResponse<StorePublicDto>.FailureResponse(
                    "Store not found");

            return ApiResponse<StorePublicDto>.SuccessResponse(
                MapToPublicDto(store));
        }

        public async Task<ApiResponse<StoreDto>> UpdateStoreAsync(
            UpdateStoreRequest request, Guid userId)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (store == null)
                return ApiResponse<StoreDto>.FailureResponse(
                    "Store not found");

            // Only update fields that were provided
            if (request.Name != null)
                store.Name = request.Name.Trim();
            if (request.Description != null)
                store.Description = request.Description;
            if (request.WhatsAppNumber != null)
                store.WhatsAppNumber = request.WhatsAppNumber;
            if (request.InstagramHandle != null)
                store.InstagramHandle = request.InstagramHandle;
            if (request.FacebookPageUrl != null)
                store.FacebookPageUrl = request.FacebookPageUrl;
            if (request.TwitterHandle != null)
                store.TwitterHandle = request.TwitterHandle;
            if (request.SupportEmail != null)
                store.SupportEmail = request.SupportEmail;
            if (request.Currency != null)
                store.Currency = request.Currency;
            if (request.CustomDomain != null)
                store.CustomDomain = request.CustomDomain;
            if (request.ThemeId != null)
                store.ThemeId = request.ThemeId;

            store.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<StoreDto>.SuccessResponse(
                await MapToStoreDtoAsync(store),
                "Store updated successfully");
        }

        public async Task<ApiResponse<StoreDto>> UpdateSlugAsync(
            UpdateStoreSlugRequest request, Guid userId)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (store == null)
                return ApiResponse<StoreDto>.FailureResponse(
                    "Store not found");

            // Check new slug availability
            var slugTaken = await _context.Stores
                .AnyAsync(s =>
                    s.Slug == request.Slug.ToLower() &&
                    s.Id != store.Id);

            if (slugTaken)
                return ApiResponse<StoreDto>.FailureResponse(
                    "This slug is already taken");

            store.Slug = request.Slug.ToLower().Trim();
            store.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<StoreDto>.SuccessResponse(
                await MapToStoreDtoAsync(store),
                "Store URL updated successfully");
        }

        public async Task<ApiResponse<StoreDto>> UploadLogoAsync(
            Guid userId, string logoUrl)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (store == null)
                return ApiResponse<StoreDto>.FailureResponse(
                    "Store not found");

            store.LogoUrl = logoUrl;
            store.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<StoreDto>.SuccessResponse(
                await MapToStoreDtoAsync(store),
                "Logo updated successfully");
        }

        public async Task<ApiResponse<StoreDto>> UploadBannerAsync(
            Guid userId, string bannerUrl)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (store == null)
                return ApiResponse<StoreDto>.FailureResponse(
                    "Store not found");

            store.BannerUrl = bannerUrl;
            store.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<StoreDto>.SuccessResponse(
                await MapToStoreDtoAsync(store),
                "Banner updated successfully");
        }

        public async Task<ApiResponse<SlugCheckDto>> CheckSlugAvailabilityAsync(
            string slug)
        {
            var isAvailable = !await _context.Stores
                .AnyAsync(s => s.Slug == slug.ToLower());

            return ApiResponse<SlugCheckDto>.SuccessResponse(new SlugCheckDto
            {
                IsAvailable = isAvailable,
                Slug = slug.ToLower()
            });
        }

        public async Task<ApiResponse> DeleteStoreAsync(Guid userId)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (store == null)
                return ApiResponse.FailureResponse("Store not found");

            store.IsDeleted = true;
            store.Status = StoreStatus.Inactive;
            store.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse("Store deleted successfully");
        }

        // ── Private Helpers ──────────────────────────────────

        private async Task<StoreDto> MapToStoreDtoAsync(Store store)
        {
            var totalProducts = await _context.Products
                .CountAsync(p => p.StoreId == store.Id);

            var orders = await _context.Orders
                .Where(o => o.StoreId == store.Id)
                .ToListAsync();

            return new StoreDto
            {
                Id = store.Id,
                Name = store.Name,
                Slug = store.Slug,
                Description = store.Description,
                LogoUrl = store.LogoUrl,
                BannerUrl = store.BannerUrl,
                ThemeId = store.ThemeId,
                CustomDomain = store.CustomDomain,
                WhatsAppNumber = store.WhatsAppNumber,
                InstagramHandle = store.InstagramHandle,
                FacebookPageUrl = store.FacebookPageUrl,
                TwitterHandle = store.TwitterHandle,
                SupportEmail = store.SupportEmail,
                Currency = store.Currency,
                Status = store.Status.ToString(),
                UserId = store.UserId,
                CreatedAt = store.CreatedAt,
                Stats = new StoreStatsDto
                {
                    TotalProducts = totalProducts,
                    TotalOrders = orders.Count,
                    TotalRevenue = orders
                        .Where(o => o.PaymentStatus == Core.Enums.PaymentStatus.Paid)
                        .Sum(o => o.Total)
                }
            };
        }

        private static StorePublicDto MapToPublicDto(Store store)
        {
            return new StorePublicDto
            {
                Name = store.Name,
                Slug = store.Slug,
                Description = store.Description,
                LogoUrl = store.LogoUrl,
                BannerUrl = store.BannerUrl,
                ThemeId = store.ThemeId,
                WhatsAppNumber = store.WhatsAppNumber,
                InstagramHandle = store.InstagramHandle,
                FacebookPageUrl = store.FacebookPageUrl,
                Currency = store.Currency
            };
        }
    }
}