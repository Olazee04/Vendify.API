using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Security.Claims;
using Vendify.Application.DTOs.Reviews;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    // GET /api/v1/reviews/product/{productId}
    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetProductReviews(
        string productId)
    {
        var result = await _reviewService
            .GetProductReviewsAsync(productId);
        return Ok(result);
    }

    // POST /api/v1/reviews — No auth needed
    [HttpPost]
    public async Task<IActionResult> CreateReview(
        [FromBody] CreateReviewDto dto)
    {
        var result = await _reviewService
            .CreateReviewAsync(dto);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    // POST /api/v1/reviews/{id}/reply — Merchant auth
    [HttpPost("{id}/reply")]
    [Authorize]
    public async Task<IActionResult> ReplyToReview(
        string id, [FromBody] ReplyReviewDto dto)
    {
        var merchantId = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(merchantId))
            return Unauthorized();

        var result = await _reviewService
            .ReplyToReviewAsync(id, merchantId, dto.Reply);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    // DELETE /api/v1/reviews/{id} — Merchant auth
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(string id)
    {
        var merchantId = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(merchantId))
            return Unauthorized();

        var result = await _reviewService
            .DeleteReviewAsync(id, merchantId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}