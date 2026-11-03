namespace PharmacyAPI.Models.RequestsModels
{
    public class OrderItemDto
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string ProductNameAr { get; set; } = string.Empty;

        // First product image only
        public string? ImageUrl { get; set; }


        // =====================================================
        // SELECTED VARIANT (SIZE)
        // =====================================================

        public int? ProductVariantId { get; set; }

        public string? SizeName { get; set; }


        // =====================================================
        // SELECTED MODIFIERS
        // =====================================================

        public List<OrderItemModifierDto> Modifiers { get; set; } = new();


        // =====================================================
        // ORDER QUANTITY & PRICE
        // =====================================================

        public int Quantity { get; set; }

        // Size price + modifiers, per one item
        public decimal UnitPrice { get; set; }

        public decimal ActualPrice { get; set; }

        public decimal TotalPrice { get; set; }
    }


    public class OrderItemModifierDto
    {
        public int? ModifierOptionId { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }
    }
}
