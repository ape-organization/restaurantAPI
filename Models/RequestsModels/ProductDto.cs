namespace PharmacyAPI.Models.RequestsModels
{
    public class ProductDto
    {
        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionEn { get; set; }

        // Base price, used when the product has no sizes
        public decimal Price { get; set; }

        public decimal ActualPrice { get; set; }

        // Used when the product has no variants
        public int StockQuantity { get; set; }

        public bool IsAvailable { get; set; } = true;

        public decimal DiscountPercentage { get; set; } = 0;

        // =========================
        // CATEGORY
        // =========================

        public int CategoryId { get; set; }

        // =========================
        // IMAGES
        // =========================

        public List<ProductImageDto> Images { get; set; }
            = new();

        // =========================
        // VARIANTS (SIZES)
        // =========================

        public List<ProductVariantDto> Variants { get; set; }
            = new();

        // =========================
        // MODIFIER GROUPS
        // =========================

        public List<ProductModifierGroupDto> ModifierGroups { get; set; }
            = new();
    }


    public class ProductImageDto
    {
        public string? ImageUrl { get; set; }

        public IFormFile? Image { get; set; }

        public int SortOrder { get; set; }
    }


    public class ProductVariantDto
    {
        public int? SizeId { get; set; }

        // Price for this size
        public decimal Price { get; set; }

        public bool IsAvailable { get; set; } = true;
    }


    // Links an existing modifier group to the product
    public class ProductModifierGroupDto
    {
        public int ModifierGroupId { get; set; }

        public bool IsRequired { get; set; } = false;

        public int MinSelections { get; set; } = 0;

        public int MaxSelections { get; set; } = 1;
    }
}
