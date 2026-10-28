using Microsoft.EntityFrameworkCore;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Reviews;
using Vendify.Application.Services.Interfaces;
using Vendify.Core.Entities;
using Vendify.Infrastructure.Data;
using Vendify.Core.Enums;

namespace Vendify.Infrastructure.Services.Implementations;

public class ReviewService : IReviewService
{
    private readonly VendifyDbContext _db;

    public ReviewService(VendifyDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ProductReviewsDto>>
        GetProductReviewsAsync(string productId)
    {
        var reviews = await _db.Reviews
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var breakdown = new Dictionary<int, int>
        {
            { 5, 0 }, { 4, 0 }, { 3, 0 },
            { 2, 0 }, { 1, 0 }
        };

        foreach (var r in reviews)
            if (breakdown.ContainsKey(r.Rating))
                breakdown[r.Rating]++;

        var avgRating = reviews.Any()
            ? reviews.Average(r => r.Rating)
            : 0;

        var dto = new ProductReviewsDto
        {
            Reviews = reviews.Select(MapToDto).ToList(),
            AverageRating = Math.Round(avgRating, 1),
            TotalReviews = reviews.Count,
            RatingBreakdown = breakdown
        };

        return ApiResponse<ProductReviewsDto>
            .SuccessResponse(dto);
    }

    public async Task<ApiResponse<ReviewDto>>
        CreateReviewAsync(CreateReviewDto dto)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
            return ApiResponse<ReviewDto>
                .FailureResponse("Rating must be 1-5");

        var store = await _db.Stores
            .FirstOrDefaultAsync(s =>
                s.Slug == dto.StoreSlug);

        if (store == null)
            return ApiResponse<ReviewDto>
                .FailureResponse("Store not found");

        // Only people who bought this product can review it
        if (string.IsNullOrWhiteSpace(dto.OrderNumber) ||
            string.IsNullOrWhiteSpace(dto.CustomerEmail))
            return ApiResponse<ReviewDto>.FailureResponse(
                "Please enter the order number and the email you used to buy this product.");

        if (!Guid.TryParse(dto.ProductId, out var productGuid))
            return ApiResponse<ReviewDto>.FailureResponse("Invalid product");

        var orderNumber = dto.OrderNumber.Trim().ToUpper();
        var email = dto.CustomerEmail.Trim().ToLower();

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o =>
                o.OrderNumber == orderNumber &&
                o.StoreId == store.Id &&
                o.CustomerEmail.ToLower() == email);

        if (order == null)
            return ApiResponse<ReviewDto>.FailureResponse(
                "We could not find an order with that order number and email.");

        if (order.PaymentStatus != PaymentStatus.Paid ||
            order.Status == OrderStatus.Cancelled)
            return ApiResponse<ReviewDto>.FailureResponse(
                "Only paid orders can be reviewed.");

        if (!order.Items.Any(i => i.ProductId == productGuid))
            return ApiResponse<ReviewDto>.FailureResponse(
                "This product is not part of that order.");

        var alreadyReviewed = await _db.Reviews.AnyAsync(r =>
            r.ProductId == dto.ProductId &&
            r.CustomerEmail.ToLower() == email);

        if (alreadyReviewed)
            return ApiResponse<ReviewDto>.FailureResponse(
                "You have already reviewed this product.");

        var review = new Review
        {
            ProductId = dto.ProductId,
            StoreId = store.Id.ToString(),
            CustomerName = dto.CustomerName,
            CustomerEmail = dto.CustomerEmail,
            Rating = dto.Rating,
            Comment = dto.Comment,
            IsVerifiedPurchase = true,
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        return ApiResponse<ReviewDto>
            .SuccessResponse(
                MapToDto(review), "Review submitted!");
    }

    public async Task<ApiResponse<bool>>
        ReplyToReviewAsync(
            string reviewId,
            string merchantId,
            string reply)
    {
        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review == null)
            return ApiResponse<bool>
                .FailureResponse("Review not found");

        // Verify merchant owns this store
        var storeExists = await _db.Stores.AnyAsync(s =>
            s.Id.ToString() == review.StoreId &&
            s.MerchantId == merchantId);

        if (!storeExists)
            return ApiResponse<bool>
                .FailureResponse("Not authorized");

        review.MerchantReply = reply;
        review.MerchantRepliedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>
            .SuccessResponse(true, "Reply posted!");
    }

    public async Task<ApiResponse<bool>>
        DeleteReviewAsync(
            string reviewId,
            string merchantId)
    {
        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review == null)
            return ApiResponse<bool>
                .FailureResponse("Review not found");

        // Verify merchant owns this store
        var storeExists = await _db.Stores.AnyAsync(s =>
            s.Id.ToString() == review.StoreId &&
            s.MerchantId == merchantId);

        if (!storeExists)
            return ApiResponse<bool>
                .FailureResponse("Not authorized");

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.SuccessResponse(true);
    }

    private static ReviewDto MapToDto(Review r) => new()
    {
        Id = r.Id,
        CustomerName = r.CustomerName,
        Rating = r.Rating,
        Comment = r.Comment,
        MerchantReply = r.MerchantReply,
        IsVerifiedPurchase = r.IsVerifiedPurchase,
        CreatedAt = r.CreatedAt,
        TimeAgo = GetTimeAgo(r.CreatedAt),
    };

    private static string GetTimeAgo(DateTime date)
    {
        var diff = DateTime.UtcNow - date;
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays}d ago";
        return date.ToString("MMM yyyy");
    }
}