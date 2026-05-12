namespace Vendify.Application.DTOs.Theme
{
    public class ThemeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PreviewImageUrl { get; set; } = string.Empty;
        public string PrimaryColor { get; set; } = string.Empty;
        public string SecondaryColor { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public bool IsPopular { get; set; }
    }

    public class ApplyThemeRequest
    {
        public string ThemeId { get; set; } = string.Empty;
    }
}