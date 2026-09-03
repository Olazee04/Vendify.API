using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vendify.Application.Common.Models;

namespace Vendify.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class AIController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AIController> _logger;

    public AIController(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<AIController> logger)
    {
        _httpClient = factory.CreateClient();
        _config = config;
        _logger = logger;
    }

    [HttpPost("generate-product-description")]
    public async Task<IActionResult> GenerateDescription(
        IFormFile image)
    {
        if (image == null || image.Length == 0)
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "No image provided"
            });

        if (image.Length > 10 * 1024 * 1024)
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Image must be less than 10MB"
            });

        var allowed = new[]
        {
            "image/jpeg", "image/png",
            "image/webp", "image/gif"
        };

        if (!allowed.Contains(image.ContentType.ToLower()))
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Only JPEG, PNG, WebP or GIF allowed"
            });

        try
        {
            // Convert image to base64
            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            var mediaType = image.ContentType;

            var apiKey = _config["Anthropic:ApiKey"];

            if (string.IsNullOrEmpty(apiKey))
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "AI service not configured"
                });

            var prompt = @"You are a professional e-commerce product copywriter for a Nigerian online store platform called Vendify.

Analyze this product image carefully and generate structured product information.

IMPORTANT RULES:
- Only describe what you can clearly see in the image
- Do NOT claim materials (leather, silk, gold, etc.) unless 100% certain — use ""leather-like"", ""appears to be"", ""synthetic"" etc.
- Keep descriptions suitable for Nigerian market (mention naira value is irrelevant here)
- Be honest and accurate — no exaggeration
- Generate in English

Return ONLY a valid JSON object with exactly these fields:
{
  ""productName"": ""Short product name (max 60 chars)"",
  ""category"": ""Most appropriate single category (e.g. Bags, Shoes, Clothing, Electronics, Hair, Accessories, Food, Beauty)"",
  ""description"": ""2-3 paragraph detailed product description (100-200 words)"",
  ""shortDescription"": ""One sentence summary (max 100 chars)"",
  ""color"": ""Primary color(s) visible"",
  ""material"": ""Material description (be honest — use 'appears to be' if uncertain)"",
  ""style"": ""Style type (e.g. Casual, Elegant, Sporty, Traditional)"",
  ""suggestedTags"": [""tag1"", ""tag2"", ""tag3"", ""tag4"", ""tag5""],
  ""seoTitle"": ""SEO-optimized product title (max 60 chars)"",
  ""confidence"": ""high|medium|low"",
  ""notes"": ""Any important caveats or things the AI could not determine from the image""
}";

            var requestBody = new
            {
                model = "claude-opus-4-5",
                max_tokens = 1024,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = mediaType,
                                    data = base64
                                }
                            },
                            new
                            {
                                type = "text",
                                text = prompt
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add(
                "x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add(
                "anthropic-version", "2023-06-01");

            var response = await _httpClient.PostAsync(
                "https://api.anthropic.com/v1/messages",
                content);

            var responseBody =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Anthropic API error: {Body}", responseBody);
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "AI service error. Please try again."
                });
            }

            // Parse Claude's response
            var claudeResponse = JsonSerializer.Deserialize
                <JsonElement>(responseBody);

            var textContent = claudeResponse
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "";

            // Extract JSON from response
            var jsonStart = textContent.IndexOf('{');
            var jsonEnd = textContent.LastIndexOf('}');

            if (jsonStart == -1 || jsonEnd == -1)
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = "Could not parse AI response"
                });

            var productJson = textContent
                .Substring(jsonStart, jsonEnd - jsonStart + 1);

            var productData = JsonSerializer
                .Deserialize<JsonElement>(productJson);

            return Ok(new
            {
                success = true,
                message = "Product description generated!",
                data = productData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI generation failed");
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = "AI generation failed. Please try again."
            });
        }
    }
}