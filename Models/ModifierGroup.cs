
namespace PharmacyAPI.Models
{
    public class ModifierGroup
    {
        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<ModifierOption> Options { get; set; }
            = new List<ModifierOption>();

        public ICollection<ProductModifierGroup> ProductModifierGroups { get; set; }
            = new List<ProductModifierGroup>();
    }
}
