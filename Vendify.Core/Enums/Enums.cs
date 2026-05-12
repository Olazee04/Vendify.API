namespace Vendify.Core.Enums
{
    public enum UserRole
    {
        Admin,
        Merchant,
        Customer
    }

    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Processing,
        Shipped,
        Delivered,
        Cancelled,
        Refunded
    }

    public enum PaymentStatus
    {
        Unpaid,
        Paid,
        Failed,
        Refunded,
        PartiallyRefunded
    }

    public enum ProductType
    {
        Physical,
        Digital,
        Course,
        Ebook
    }

    public enum StoreStatus
    {
        Active,
        Suspended,
        Inactive
    }

    public enum ShippingType
    {
        FlatRate,
        FreeShipping,
        ByWeight,
        ByLocation
    }

    public enum DiscountType
    {
        Percentage,
        FixedAmount
    }

    public enum PaymentMethod
    {
        Paystack,
        Flutterwave,
        Stripe,
        BankTransfer,
        CashOnDelivery
    }
}