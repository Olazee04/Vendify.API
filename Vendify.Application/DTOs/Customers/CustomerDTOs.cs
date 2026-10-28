namespace Vendify.Application.DTOs.Customers
{
    public static class CustomerMessages
    {
        public const string SessionExpired =
            "Your session has expired. Please sign in again.";

        public const string NotVerified =
            "Please verify your email first. We have sent you a new code.";
    }

    public class CustomerRegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CustomerVerifyRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class CustomerEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class CustomerLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CustomerResetRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CustomerChangePasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CustomerTokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public class CustomerSessionDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class CustomerProfileDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class CustomerOrderSummaryDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int ItemCount { get; set; }
    }

    // Used inside the server to know who is signed in
    public class CustomerIdentity
    {
        public Guid CustomerId { get; set; }
        public Guid StoreId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}