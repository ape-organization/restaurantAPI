
namespace PharmacyAPI.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        // Product
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // Size
        public int? SizeId { get; set; }
        public Size? Size { get; set; }

       

        // Price for this specific size/variant
        public decimal Price { get; set; }

        // Availability
        public bool IsActive { get; set; } = true;
        public bool InStock { get; set; } = true;

        // Optional inventory tracking
        public int StockQuantity { get; set; }
    }
}
