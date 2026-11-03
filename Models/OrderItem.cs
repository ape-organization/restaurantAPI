namespace PharmacyAPI.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order Order { get; set; } = null!;


        // =====================================================
        // PRODUCT
        // =====================================================

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;


        // =====================================================
        // PRODUCT VARIANT (SIZE)
        // =====================================================
        //
        // Nullable because a product may have no variants.
        //
        // Example:
        //
        // Product without sizes:
        //     ProductVariantId = null
        //
        // Product with size (Small / Medium / Large):
        //     ProductVariantId = 15
        //
        // =====================================================

        public int? ProductVariantId { get; set; }

        public ProductVariant? ProductVariant { get; set; }


        // =====================================================
        // QUANTITY
        // =====================================================

        public int Quantity { get; set; }


        // =====================================================
        // PRICE AT TIME OF ORDER
        // =====================================================
        //
        // UnitPrice = size/base price (after discount)
        //           + selected modifiers
        //
        // =====================================================

        public decimal UnitPrice { get; set; }

        public decimal ActualPrice { get; set; }


        // =====================================================
        // SELECTED MODIFIERS (extras, sauces, ...)
        // =====================================================

        public ICollection<OrderItemModifier> Modifiers { get; set; }
            = new List<OrderItemModifier>();
    }
}
