using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Theme;
using Vendify.Application.Services.Interfaces;
using Vendify.Infrastructure.Data;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class ThemeService : IThemeService
    {
        private readonly VendifyDbContext _context;

        // Built-in themes — no database needed
        private static readonly List<ThemeDto> _themes = new()
        {
            new ThemeDto
            {
                Id = "minimal",
                Name = "Minimal",
                Description = "Clean and simple design for any store",
                PreviewImageUrl = "https://res.cloudinary.com/vendify/themes/minimal.png",
                PrimaryColor = "#000000",
                SecondaryColor = "#ffffff",
                Category = "General",
                IsFree = true,
                IsPopular = true
            },
            new ThemeDto
            {
                Id = "fashion",
                Name = "Fashion",
                Description = "Elegant design for fashion stores",
                PreviewImageUrl = "https://res.cloudinary.com/vendify/themes/fashion.png",
                PrimaryColor = "#C9A96E",
                SecondaryColor = "#1A1A2E",
                Category = "Fashion",
                IsFree = true,
                IsPopular = true
            },
            new ThemeDto
            {
                Id = "tech",
                Name = "Tech Store",
                Description = "Modern design for gadgets and electronics",
                PreviewImageUrl = "https://res.cloudinary.com/vendify/themes/tech.png",
                PrimaryColor = "#0066FF",
                SecondaryColor = "#00D4FF",
                Category = "Electronics",
                IsFree = true,
                IsPopular = false
            },
            new ThemeDto
            {
                Id = "food",
                Name = "Food & Drinks",
                Description = "Warm and inviting design for food stores",
                PreviewImageUrl = "https://res.cloudinary.com/vendify/themes/food.png",
                PrimaryColor = "#FF6B35",
                SecondaryColor = "#F7C59F",
                Category = "Food",
                IsFree = true,
                IsPopular = true
            },
            new ThemeDto
            {
                Id = "beauty",
                Name = "Beauty & Wellness",
                Description = "Soft and elegant design for beauty stores",
                PreviewImageUrl = "https://res.cloudinary.com/vendify/themes/beauty.png",
                PrimaryColor = "#E91E8C",
                SecondaryColor = "#FFF0F7",
                Category = "Beauty",
                IsFree = true,
                IsPopular = true
            },
            new ThemeDto
            {
                Id = "digital",
                Name = "Digital Products",
                Description = "Perfect for ebooks, courses and digital goods",
                PreviewImageUrl = "https://res.cloudinary.com/vendify/themes/digital.png",
                PrimaryColor = "#6C63FF",
                SecondaryColor = "#F0EEFF",
                Category = "Digital",
                IsFree = true,
                IsPopular = false
            },
            new ThemeDto
            {
                Id = "naija",
                Name = "Naija Market",
                Description = "Bold and vibrant design for Nigerian stores",
                PreviewImageUrl = "https://res.cloudinary.com/vendify/themes/naija.png",
                PrimaryColor = "#008751",
                SecondaryColor = "#FFD700",
                Category = "General",
                IsFree = true,
                IsPopular = true
            }
        };

        public ThemeService(VendifyDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<ThemeDto>>> GetAllThemesAsync()
        {
            await Task.CompletedTask;
            return ApiResponse<List<ThemeDto>>.SuccessResponse(_themes);
        }

        public async Task<ApiResponse<ThemeDto>> GetThemeByIdAsync(
            string themeId)
        {
            await Task.CompletedTask;
            var theme = _themes.FirstOrDefault(t =>
                t.Id == themeId.ToLower());

            if (theme == null)
                return ApiResponse<ThemeDto>.FailureResponse(
                    "Theme not found");

            return ApiResponse<ThemeDto>.SuccessResponse(theme);
        }

        public async Task<ApiResponse<ThemeDto>> ApplyThemeAsync(
            string themeId, Guid userId)
        {
            var theme = _themes.FirstOrDefault(t =>
                t.Id == themeId.ToLower());

            if (theme == null)
                return ApiResponse<ThemeDto>.FailureResponse(
                    "Theme not found");

            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (store == null)
                return ApiResponse<ThemeDto>.FailureResponse(
                    "Store not found");

            store.ThemeId = themeId.ToLower();
            store.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<ThemeDto>.SuccessResponse(
                theme, $"Theme '{theme.Name}' applied successfully");
        }
    }
}