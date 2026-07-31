using Microsoft.AspNetCore.Mvc;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers;

[ApiController]
[Route("api/v1/whatsapp")]
public class WhatsAppBotController : ControllerBase
{
    private readonly IWhatsAppBotService _botService;
    private readonly IConfiguration _config;

    public WhatsAppBotController(
        IWhatsAppBotService botService,
        IConfiguration config)
    {
        _botService = botService;
        _config = config;
    }

    // Webhook verification by Meta
    [HttpGet("webhook/{storeSlug}")]
    public IActionResult Verify(
        string storeSlug,
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string token)
    {
        var verifyToken = _config["WhatsApp:VerifyToken"]
            ?? "vendify_verify_token";

        if (mode == "subscribe" && token == verifyToken)
            return Ok(challenge);

        return Forbid();
    }

    // Receive incoming WhatsApp messages
    [HttpPost("webhook/{storeSlug}")]
    public async Task<IActionResult> Receive(
        string storeSlug,
        [FromBody] object payload)
    {
        await _botService
            .ProcessMessageAsync(storeSlug, payload);
        return Ok();
    }
}