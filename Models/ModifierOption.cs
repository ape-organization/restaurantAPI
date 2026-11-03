
namespace PharmacyAPI.Models
{
    public class ModifierOption
    {
        public int Id { get; set; }

        public int ModifierGroupId { get; set; }
        public ModifierGroup ModifierGroup { get; set; } = null!;

        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;

        // Added to the item price when this option is selected
        public decimal Price { get; set; } = 0;

        // Optional: the option is an existing menu product (e.g. "Add Fries")
        public int? ExtraProductId { get; set; }
        public Product? ExtraProduct { get; set; }

        // Temporarily unavailable (e.g. ran out today)
        public bool IsAvailable { get; set; } = true;

        // Soft delete
        public bool IsActive { get; set; } = true;
    }
}
