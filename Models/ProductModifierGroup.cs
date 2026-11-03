
namespace PharmacyAPI.Models
{
    public class ProductModifierGroup
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int ModifierGroupId { get; set; }
        public ModifierGroup ModifierGroup { get; set; } = null!;

        public bool IsRequired { get; set; } = false;

        public int MinSelections { get; set; } = 0;
        public int MaxSelections { get; set; } = 1;
    }
}
