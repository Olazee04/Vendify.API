using Vendify.Core.Enums;
using static System.Formats.Asn1.AsnWriter;

namespace Vendify.Core.Entities
{
    public class User : BaseEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public UserRole Role { get; set; } = UserRole.Merchant;
        public bool IsVerified { get; set; } = false;
        public string? VerificationToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpiry { get; set; }

        // Navigation
        public Store? Store { get; set; }
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}