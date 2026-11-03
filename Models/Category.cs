namespace PharmacyAPI.Models
{
    public class Category
    {
        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }
        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Products belonging to this category
        public ICollection<Product> Products { get; set; }
            = new List<Product>();
    }
}