namespace PharmacyAPI.Models.RequestsModels
{
    public class ProductResponseDto
    {
        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionEn { get; set; }

        public decimal Price { get; set; }

        public decimal ActualPrice { get; set; }

        // Used when the product has no variants
        public int StockQuantity { get; set; }

        public bool IsAvailable { get; set; }

        public bool HasVariants { get; set; }

        public bool HasModifiers { get; set; }

        public decimal DiscountPercentage { get; set; }

        public decimal DiscountedPrice { get; set; }

        // =========================
        // CATEGORY
        // =========================

        public int CategoryId { get; set; }

        public CategoryResponseDto? Category { get; set; }

        // =========================
        // IMAGES
        // =========================

        public List<string> Images { get; set; }
            = new();

        // =========================
        // VARIANTS (SIZES)
        // =========================

        public List<ProductVariantResponseDto> Variants { get; set; }
            = new();

        // =========================
        // MODIFIER GROUPS
        // =========================

        public List<ProductModifierGroupResponseDto> ModifierGroups { get; set; }
            = new();
    }


    public class CategoryResponseDto
    {
        public int Id { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;
    }


    public class ProductImageResponseDto
    {
        public int Id { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }


    public class ProductVariantResponseDto
    {
        public int Id { get; set; }

        public int? SizeId { get; set; }

        public string? SizeName { get; set; }

        public decimal Price { get; set; }

        // Price after the product discount
        public decimal DiscountedPrice { get; set; }

        public bool IsAvailable { get; set; }

        public int StockQuantity { get; set; }
    }


    public class ProductModifierGroupResponseDto
    {
        public int ModifierGroupId { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public bool IsRequired { get; set; }

        public int MinSelections { get; set; }

        public int MaxSelections { get; set; }

        public List<ModifierOptionResponseDto> Options { get; set; }
            = new();
    }


    public class ModifierOptionResponseDto
    {
        public int Id { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public bool IsAvailable { get; set; }

        public int? ExtraProductId { get; set; }
    }
}
