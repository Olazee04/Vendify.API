namespace Vendify.Core.Entities
{
    // A shopper's account at ONE store
    public class StoreCustomer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StoreId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockedUntil { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // A one-time emailed code. Purpose is "Verify" or "Reset".
    public class CustomerAuthCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StoreId { get; set; }
        public Guid CustomerId { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}