namespace Vendify.Application.DTOs.Reviews;

public class CreateReviewDto
{
    public string ProductId { get; set; } = string.Empty;
    public string StoreSlug { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
}

public class ReplyReviewDto
{
    public string Reply { get; set; } = string.Empty;
}

public class ReviewDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string? MerchantReply { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}

public class ProductReviewsDto
{
    public List<ReviewDto> Reviews { get; set; } = new();
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingBreakdown { get; set; }
        = new();
}