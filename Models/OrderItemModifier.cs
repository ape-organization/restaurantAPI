
namespace PharmacyAPI.Models
{
    public class OrderItemModifier
    {
        public int Id { get; set; }

        // Order item
        public int OrderItemId { get; set; }
        public OrderItem OrderItem { get; set; } = null!;

        // Original option reference (optional for historical records)
        public int? ModifierOptionId { get; set; }
        public ModifierOption? ModifierOption { get; set; }

        // Snapshot: preserve the option details at purchase time
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; } = 1;
    }
}
