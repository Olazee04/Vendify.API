using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Theme;

namespace Vendify.Application.Services.Interfaces
{
    public interface IThemeService
    {
        Task<ApiResponse<List<ThemeDto>>> GetAllThemesAsync();
        Task<ApiResponse<ThemeDto>> GetThemeByIdAsync(string themeId);
        Task<ApiResponse<ThemeDto>> ApplyThemeAsync(
            string themeId, Guid userId);
    }
}