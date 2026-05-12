using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.DTOs.Theme;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ThemesController : ControllerBase
    {
        private readonly IThemeService _themeService;

        public ThemesController(IThemeService themeService)
        {
            _themeService = themeService;
        }

        // GET /api/v1/themes — Public
        [HttpGet]
        public async Task<IActionResult> GetAllThemes()
        {
            var result = await _themeService.GetAllThemesAsync();
            return Ok(result);
        }

        // GET /api/v1/themes/{id} — Public
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTheme(string id)
        {
            var result = await _themeService.GetThemeByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // POST /api/v1/themes/apply — Auth required
        [Authorize]
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyTheme(
            [FromBody] ApplyThemeRequest request)
        {
            var userId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _themeService
                .ApplyThemeAsync(request.ThemeId, userId);

            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}