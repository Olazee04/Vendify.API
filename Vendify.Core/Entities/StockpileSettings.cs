namespace Vendify.Core.Entities
{
    public class StockpileSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StoreId { get; set; }
        public bool IsEnabled { get; set; }

        // Limits. Empty (null) means "no limit of this kind".
        // The first limit reached stops further stockpiling.
        public int? MaxItems { get; set; }
        public decimal? MaxAmount { get; set; }
        public decimal? MaxWeightKg { get; set; }

        // How many days a customer can keep a stockpile before it expires
        public int? HoldDays { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}