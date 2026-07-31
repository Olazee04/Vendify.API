namespace Vendify.Core.Entities;

public class Review
{
    public string Id { get; set; } =
        Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5
    public string Comment { get; set; } = string.Empty;
    public string? MerchantReply { get; set; }
    public DateTime? MerchantRepliedAt { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    //public Product? Product { get; set; }
    //public Store? Store { get; set; }
}