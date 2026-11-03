namespace PharmacyAPI.Models.RequestsModels
{
    // Used for both requests and responses of the modifier groups API.
    public class ModifierGroupDto
    {
        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public List<ModifierOptionDto> Options { get; set; }
            = new();
    }


    public class ModifierOptionDto
    {
        // 0 / null = new option, otherwise updates the existing option
        public int? Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int? ExtraProductId { get; set; }
    }
}
