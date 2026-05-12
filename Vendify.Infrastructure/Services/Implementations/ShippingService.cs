using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Shipping;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Core.Enums;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class ShippingService : IShippingService
    {
        private readonly VendifyDbContext _context;

        public ShippingService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<ShippingZoneDto>> CreateZoneAsync(
            CreateShippingZoneRequest request, Guid storeId)
        {
            // Check for duplicate zone name in store
            var exists = await _context.ShippingZones
                .AnyAsync(z =>
                    z.StoreId == storeId &&
                    z.Name.ToLower() == request.Name.ToLower());

            if (exists)
                return ApiResponse<ShippingZoneDto>.FailureResponse(
                    "A shipping zone with this name already exists");

            var zone = new ShippingZone
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                Fee = request.Fee,
                Type = request.Type,
                IsActive = request.IsActive,
                EstimatedDaysMin = request.EstimatedDaysMin,
                EstimatedDaysMax = request.EstimatedDaysMax,
                StoreId = storeId
            };

            _context.ShippingZones.Add(zone);
            await _context.SaveChangesAsync();

            return ApiResponse<ShippingZoneDto>.SuccessResponse(
                MapToDto(zone), "Shipping zone created successfully");
        }

        public async Task<ApiResponse<List<ShippingZoneDto>>>
            GetStoreZonesAsync(Guid storeId)
        {
            var zones = await _context.ShippingZones
                .Where(z => z.StoreId == storeId)
                .OrderBy(z => z.Name)
                .ToListAsync();

            return ApiResponse<List<ShippingZoneDto>>.SuccessResponse(
                zones.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<List<ShippingZoneDto>>>
            GetPublicStoreZonesAsync(string storeSlug)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Slug == storeSlug.ToLower());

            if (store == null)
                return ApiResponse<List<ShippingZoneDto>>.FailureResponse(
                    "Store not found");

            var zones = await _context.ShippingZones
                .Where(z => z.StoreId == store.Id && z.IsActive)
                .OrderBy(z => z.Fee)
                .ToListAsync();

            return ApiResponse<List<ShippingZoneDto>>.SuccessResponse(
                zones.Select(MapToDto).ToList());
        }

        public async Task<ApiResponse<ShippingZoneDto>> UpdateZoneAsync(
            Guid zoneId, UpdateShippingZoneRequest request, Guid storeId)
        {
            var zone = await _context.ShippingZones
                .FirstOrDefaultAsync(z =>
                    z.Id == zoneId && z.StoreId == storeId);

            if (zone == null)
                return ApiResponse<ShippingZoneDto>.FailureResponse(
                    "Shipping zone not found");

            if (request.Name != null) zone.Name = request.Name.Trim();
            if (request.Description != null)
                zone.Description = request.Description;
            if (request.Fee.HasValue) zone.Fee = request.Fee.Value;
            if (request.Type.HasValue) zone.Type = request.Type.Value;
            if (request.IsActive.HasValue)
                zone.IsActive = request.IsActive.Value;
            if (request.EstimatedDaysMin.HasValue)
                zone.EstimatedDaysMin = request.EstimatedDaysMin;
            if (request.EstimatedDaysMax.HasValue)
                zone.EstimatedDaysMax = request.EstimatedDaysMax;

            zone.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<ShippingZoneDto>.SuccessResponse(
                MapToDto(zone), "Shipping zone updated successfully");
        }

        public async Task<ApiResponse<ShippingCalculateDto>>
            CalculateShippingAsync(CalculateShippingRequest request)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s =>
                    s.Slug == request.StoreSlug.ToLower());

            if (store == null)
                return ApiResponse<ShippingCalculateDto>.FailureResponse(
                    "Store not found");

            var zones = await _context.ShippingZones
                .Where(z => z.StoreId == store.Id && z.IsActive)
                .ToListAsync();

            if (!zones.Any())
                return ApiResponse<ShippingCalculateDto>.SuccessResponse(
                    new ShippingCalculateDto
                    {
                        Fee = 0,
                        ZoneName = "Standard",
                        IsFreeShipping = true,
                        DeliveryEstimate = "3-7 business days"
                    });

            // Match zone by state name
            var matchedZone = zones.FirstOrDefault(z =>
                z.Name.ToLower().Contains(
                    request.State.ToLower()) ||
                request.State.ToLower().Contains(
                    z.Name.ToLower()))

                // Fallback to nationwide zone
                ?? zones.FirstOrDefault(z =>
                    z.Name.ToLower() == "nationwide" ||
                    z.Name.ToLower() == "all states" ||
                    z.Name.ToLower() == "all")

                // Fallback to international
                ?? zones.FirstOrDefault(z =>
                    z.Name.ToLower() == "international" &&
                    request.Country?.ToLower() != "nigeria")

                // Use cheapest available
                ?? zones.OrderBy(z => z.Fee).FirstOrDefault();

            if (matchedZone == null)
                return ApiResponse<ShippingCalculateDto>.SuccessResponse(
                    new ShippingCalculateDto
                    {
                        Fee = 0,
                        ZoneName = "Standard",
                        IsFreeShipping = true
                    });

            return ApiResponse<ShippingCalculateDto>.SuccessResponse(
                new ShippingCalculateDto
                {
                    Fee = matchedZone.Fee,
                    ZoneName = matchedZone.Name,
                    IsFreeShipping = matchedZone.Type ==
                        ShippingType.FreeShipping || matchedZone.Fee == 0,
                    DeliveryEstimate = GetDeliveryEstimate(matchedZone)
                });
        }

        public async Task<ApiResponse> DeleteZoneAsync(
            Guid zoneId, Guid storeId)
        {
            var zone = await _context.ShippingZones
                .FirstOrDefaultAsync(z =>
                    z.Id == zoneId && z.StoreId == storeId);

            if (zone == null)
                return ApiResponse.FailureResponse(
                    "Shipping zone not found");

            _context.ShippingZones.Remove(zone);
            await _context.SaveChangesAsync();

            return ApiResponse.SuccessResponse(
                "Shipping zone deleted successfully");
        }

        // ── Private Helpers ──────────────────────────────────

        private static ShippingZoneDto MapToDto(ShippingZone zone)
        {
            return new ShippingZoneDto
            {
                Id = zone.Id,
                Name = zone.Name,
                Description = zone.Description,
                Fee = zone.Fee,
                Type = zone.Type.ToString(),
                IsActive = zone.IsActive,
                EstimatedDaysMin = zone.EstimatedDaysMin,
                EstimatedDaysMax = zone.EstimatedDaysMax,
                DeliveryEstimate = GetDeliveryEstimate(zone),
                StoreId = zone.StoreId
            };
        }

        private static string GetDeliveryEstimate(ShippingZone zone)
        {
            if (zone.EstimatedDaysMin.HasValue &&
                zone.EstimatedDaysMax.HasValue)
                return $"{zone.EstimatedDaysMin}-" +
                    $"{zone.EstimatedDaysMax} business days";

            if (zone.EstimatedDaysMin.HasValue)
                return $"{zone.EstimatedDaysMin}+ business days";

            return zone.Name.ToLower() == "lagos"
                ? "1-2 business days"
                : zone.Name.ToLower() == "international"
                    ? "7-14 business days"
                    : "3-7 business days";
        }
    }
}