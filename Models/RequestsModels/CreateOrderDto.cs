namespace PharmacyAPI.Models.RequestsModels
{
    public class CreateOrderDto
    {
        public ClientOrderDto Client { get; set; } = null!;

        public List<CreateOrderItemDto> Items { get; set; } = new();
        public string? Note { get; set; }
    }


    // =========================================================
    // CLIENT
    // =========================================================

    public class ClientOrderDto
    {
        public string Name { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string Address { get; set; } = string.Empty;
    }


    // =========================================================
    // ORDER ITEM
    // =========================================================

    public class CreateOrderItemDto
    {
        public int ProductId { get; set; }

        // Nullable because some products have no variants.
        public int? ProductVariantId { get; set; }

        public int Quantity { get; set; }

        // Selected extras / sauces / add-ons
        public List<CreateOrderItemModifierDto> Modifiers { get; set; } = new();

        // Unit price displayed to the customer on checkout
        // (size price + modifiers, after discount).
        // Used ONLY to detect price changes.
        public decimal? DisplayedUnitPrice { get; set; }
    }

    public class CreateOrderItemModifierDto
    {
        public int ModifierOptionId { get; set; }

        // How many of this option per one item (e.g. 2x extra cheese)
        public int Quantity { get; set; } = 1;
    }

    public class PriceChangeItemDto
    {
        public int ProductId { get; set; }

        public int? ProductVariantId { get; set; }

        public decimal OldPrice { get; set; }

        public decimal NewPrice { get; set; }
    }
}