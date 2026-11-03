
using RestaurantAPI.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Models.Responses;
using System.Linq.Expressions;
using System.Net;
using System.Text;

namespace PharmacyAPI.Services
{
    public interface IOrderService
    {
         Task<bool> CompleteOrder(
          int id,
          CancellationToken cancellationToken = default);
        Task<OrderDto> CreateOrder(
            CreateOrderDto dto,
            CancellationToken cancellationToken = default);

        Task<OrderDto?> GetOrder(
            int id,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<OrderDto>> GetOrders(
            int page = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default);

        Task<List<OrderDto>> GetOrdersByClient(
            int clientId,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateOrderStatus(
            int id,
            string status,
            CancellationToken cancellationToken = default);

        Task<bool> CancelOrder(
            int id,
            CancellationToken cancellationToken = default);

        Task<DashboardStatsDto> GetCurrentMonthStats(
            CancellationToken cancellationToken = default);

        Task<DashboardStatsDto> GetTotalStats(
            CancellationToken cancellationToken = default);
    }


    public sealed class OrderService : IOrderService
    {
        private const string DefaultAdminEmail = "deaved.makram@gmail.com";
       // private const string DefaultAdminEmail = "ereny.karam.noshy@gmail.com";
        // SQL Server unique index violations.
        private const int SqlUniqueIndexViolation = 2601;
        private const int SqlUniqueConstraintViolation = 2627;

        private readonly ShoesDbContext _context;
        private readonly IEmailQueue _emailQueue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            ShoesDbContext context,
            IEmailQueue emailQueue,
            IConfiguration configuration,
            ILogger<OrderService> logger)
        {
            _context = context;
            _emailQueue = emailQueue;
            _configuration = configuration;
            _logger = logger;
        }


        // =========================================================
        // CREATE ORDER
        // =========================================================

        public async Task<OrderDto> CreateOrder(
      CreateOrderDto dto,
      CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);


            // =====================================================
            // VALIDATE CLIENT
            // =====================================================

            if (dto.Client == null)
            {
                throw new InvalidOperationException(
                    "معلومات العميل غير متوفره.");
            }


            var clientName =
                dto.Client.Name?.Trim();


            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException(
                    "اسم العميل مطلوب.");
            }


            var phoneNumber =
                dto.Client.PhoneNumber?.Trim();


            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                throw new InvalidOperationException(
                    "رقم العميل مطلوب.");
            }


            var address =
                dto.Client.Address?.Trim();


            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    "عنوان العميل مطلوب.");
            }


            // =====================================================
            // VALIDATE ITEMS
            // =====================================================

            if (dto.Items == null || dto.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    "الطلب يجب ان يحتوي علي الاقل علي منتج واحد.");
            }


            foreach (var item in dto.Items)
            {
                if (item.ProductId <= 0)
                {
                    throw new InvalidOperationException(
                        "منتج غير متوفر.");
                }


                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException(
                        "الكميه يجب ان تكون اكبر من الصفر.");
                }


                if (item.ProductVariantId.HasValue &&
                    item.ProductVariantId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "الاختيار الخاص بالمنتج غير صحيح.");
                }


                // =================================================
                // VALIDATE DISPLAYED PRICE
                // =================================================

                if (item.DisplayedUnitPrice.HasValue &&
                    item.DisplayedUnitPrice.Value < 0)
                {
                    throw new InvalidOperationException(
                        "سعر المنتج غير صحيح.");
                }
            }


            // =====================================================
            // MERGE DUPLICATE ITEMS
            // =====================================================
            //
            // Lines are merged only when product, size AND the
            // selected modifiers are identical. A burger with
            // extra cheese and a plain burger stay separate lines.
            //
            // =====================================================

            var requestedItems =
                dto.Items
                    .Select(x => new RequestedOrderItem
                    {
                        ProductId =
                            x.ProductId,

                        ProductVariantId =
                            x.ProductVariantId,

                        Quantity =
                            x.Quantity,

                        Modifiers =
                            NormalizeModifiers(x.Modifiers),

                        DisplayedUnitPrice =
                            x.DisplayedUnitPrice
                    })
                    .GroupBy(x => new
                    {
                        x.ProductId,
                        x.ProductVariantId,
                        ModifiersKey = BuildModifiersKey(x.Modifiers)
                    })
                    .Select(g => new RequestedOrderItem
                    {
                        ProductId =
                            g.Key.ProductId,

                        ProductVariantId =
                            g.Key.ProductVariantId,

                        Quantity =
                            g.Sum(x => x.Quantity),

                        Modifiers =
                            g.First().Modifiers,

                        DisplayedUnitPrice =
                            g.Select(x => x.DisplayedUnitPrice)
                                .FirstOrDefault()
                    })
                    .ToList();


            // =====================================================
            // VALIDATE MERGED QUANTITIES
            // =====================================================

            if (requestedItems.Any(x => x.Quantity <= 0))
            {
                throw new InvalidOperationException(
                    "الكميه غير صحيحه.");
            }


            // =====================================================
            // PRODUCT IDS
            // =====================================================

            var productIds =
                requestedItems
                    .Select(x => x.ProductId)
                    .Distinct()
                    .ToList();


            // =====================================================
            // LOAD PRODUCTS
            // =====================================================
            //
            // Only active variants and active modifier options
            // are loaded.
            //
            // No stock is checked, decreased,
            // or changed here.
            //
            // =====================================================

            var products =
                await _context.Products
                    .AsNoTracking()
                    .Where(p =>
                        productIds.Contains(p.Id) &&
                        !p.IsDeleted)
                    .Include(p =>
                        p.Variants
                            .Where(v => v.IsActive))
                        .ThenInclude(v => v.Size)
                    .Include(p => p.ProductModifierGroups)
                        .ThenInclude(pg => pg.ModifierGroup)
                        .ThenInclude(g => g.Options
                            .Where(o => o.IsActive))
                    .AsSplitQuery()
                    .ToDictionaryAsync(
                        p => p.Id,
                        cancellationToken);


            // =====================================================
            // CHECK PRODUCTS EXIST
            // =====================================================

            if (products.Count != productIds.Count)
            {
                var missingProductId =
                    productIds.First(
                        id => !products.ContainsKey(id));


                throw new KeyNotFoundException(
                    $"المنتج {missingProductId} غير موجود.");
            }


            // =====================================================
            // PRICE EVERY ITEM
            // =====================================================
            //
            // IMPORTANT:
            //
            // The price is calculated from the database:
            //
            // size price (or product price) after discount
            // + selected modifiers
            //
            // It is NOT the price sent by Angular.
            //
            // =====================================================

            var pricedItems =
                requestedItems
                    .Select(x => PriceItem(
                        x,
                        products[x.ProductId]))
                    .ToList();


            // =====================================================
            // CHECK PRICE CHANGES
            // =====================================================
            //
            // The customer sent the price that was displayed
            // on the checkout page.
            //
            // We compare it with the CURRENT database price.
            //
            // The displayed price is NEVER used as the actual
            // order price.
            //
            // =====================================================

            var priceChanges =
                new List<PriceChangeItemDto>();


            foreach (var pricedItem in pricedItems)
            {
                var displayedPrice =
                    pricedItem.Request.DisplayedUnitPrice;


                // -------------------------------------------------
                // If the frontend did not send a displayed price,
                // do not block the order.
                //
                // The backend price is still authoritative.
                // -------------------------------------------------

                if (!displayedPrice.HasValue)
                {
                    continue;
                }


                // -------------------------------------------------
                // Compare prices
                // -------------------------------------------------

                if (Math.Round(
                        displayedPrice.Value,
                        2,
                        MidpointRounding.AwayFromZero) != pricedItem.UnitPrice)
                {
                    priceChanges.Add(
                        new PriceChangeItemDto
                        {
                            ProductId =
                                pricedItem.Product.Id,

                            ProductVariantId =
                                pricedItem.Variant?.Id,

                            OldPrice =
                                displayedPrice.Value,

                            NewPrice =
                                pricedItem.UnitPrice
                        });
                }
            }


            // =====================================================
            // STOP ORDER IF PRICE CHANGED
            // =====================================================

            if (priceChanges.Count > 0)
            {
                throw new PriceChangedException(
                    "تغير سعر بعض المنتجات. يرجى مراجعة الطلب قبل إتمام الشراء.",
                    priceChanges);
            }


            // =====================================================
            // FIND CLIENT
            // =====================================================

            var client =
                await _context.Clients
                    .FirstOrDefaultAsync(
                        c => c.PhoneNumber == phoneNumber,
                        cancellationToken);


            var now =
                DateTime.UtcNow;


            // =====================================================
            // CLIENT EMAIL
            // =====================================================

            var email =
                string.IsNullOrWhiteSpace(dto.Client.Email)
                    ? null
                    : dto.Client.Email.Trim();


            // =====================================================
            // CREATE / UPDATE CLIENT
            // =====================================================

            var isNewClient =
                client == null;

            if (client == null)
            {
                client = new Client
                {
                    Name =
                        clientName,

                    PhoneNumber =
                        phoneNumber,

                    Address =
                        address,

                    Email =
                        email,

                    CreatedAt =
                        now
                };


                _context.Clients.Add(client);
            }
            else
            {
                UpdateClientDetails(
                    client,
                    clientName,
                    address,
                    email,
                    now);
            }


            // =====================================================
            // CREATE ORDER
            // =====================================================

            var order =
                new Order
                {
                    Client =
                        client,

                    OrderDate =
                        now,

                    Status =
                        OrderStatus.Pending,

                    Note=
                        dto.Note?.Trim(),
                    TotalAmount =
                        0
                };


            decimal total = 0;


            // =====================================================
            // CREATE ORDER ITEMS
            // =====================================================

            foreach (var pricedItem in pricedItems)
            {
                var quantity =
                    pricedItem.Request.Quantity;


                total +=
                    pricedItem.UnitPrice *
                    quantity;


                var orderItem =
                    new OrderItem
                    {
                        ProductId =
                            pricedItem.Product.Id,

                        ProductVariantId =
                            pricedItem.Variant?.Id,

                        Quantity =
                            quantity,

                        UnitPrice =
                            pricedItem.UnitPrice,

                        ActualPrice =
                            pricedItem.Product.ActualPrice
                    };


                // -------------------------------------------------
                // Snapshot the selected modifiers, so the order
                // keeps its names and prices even if the menu
                // changes later.
                // -------------------------------------------------

                foreach (var (option, modifierQuantity) in pricedItem.Modifiers)
                {
                    orderItem.Modifiers.Add(
                        new OrderItemModifier
                        {
                            ModifierOptionId =
                                option.Id,

                            NameAr =
                                option.NameAr,

                            NameEn =
                                option.NameEn,

                            UnitPrice =
                                option.Price,

                            Quantity =
                                modifierQuantity
                        });
                }


                order.Items.Add(orderItem);
            }


            // =====================================================
            // FINAL ORDER TOTAL
            // =====================================================

            order.TotalAmount =
                Math.Round(
                    total,
                    2,
                    MidpointRounding.AwayFromZero);


            // =====================================================
            // ADD ORDER
            // =====================================================

            _context.Orders.Add(order);


            // =====================================================
            // SAVE EVERYTHING
            // =====================================================

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException ex) when (
                isNewClient &&
                IsUniqueViolation(ex))
            {
                // Another request created a client with the same
                // phone number at the same time. Attach the order
                // to that client and save again.
                _context.Entry(client).State =
                    EntityState.Detached;

                client =
                    await _context.Clients
                        .FirstOrDefaultAsync(
                            c => c.PhoneNumber == phoneNumber,
                            cancellationToken)
                    ?? throw new InvalidOperationException(
                        "تعذر حفظ بيانات العميل.", ex);

                UpdateClientDetails(
                    client,
                    clientName,
                    address,
                    email,
                    now);

                order.Client =
                    client;

                await _context.SaveChangesAsync(
                    cancellationToken);
            }


            // =====================================================
            // QUEUE ADMIN EMAIL
            // =====================================================
            //
            // Sent by a background service, so the checkout
            // response never waits for the mail API and a mail
            // failure can never fail or crash the order.
            //
            // =====================================================

            try
            {
                var adminEmail =
                    _configuration["OrderNotifications:AdminEmail"];

                var queued =
                    _emailQueue.TryEnqueue(
                        new EmailMessage(
                            string.IsNullOrWhiteSpace(adminEmail)
                                ? DefaultAdminEmail
                                : adminEmail,
                            $"إشعار طلب جديد رقم #{order.Id}",
                            BuildOrderEmailBody(client, products, order)));

                if (!queued)
                {
                    _logger.LogWarning(
                        "Email queue is full. Order {OrderId} email was not queued.",
                        order.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to queue email for order {OrderId}.",
                    order.Id);
            }

            return await GetOrder(
                       order.Id,
                       cancellationToken)
                   ?? throw new InvalidOperationException(
                       "تعذر تحميل الطلب بعد حفظه.");
        }


        // =========================================================
        // CLIENT HELPERS
        // =========================================================

        private static void UpdateClientDetails(
            Client client,
            string clientName,
            string address,
            string? email,
            DateTime now)
        {
            client.Name =
                clientName;

            client.Address =
                address;

            // Keep the saved email when the customer
            // leaves the field empty.
            if (email != null)
            {
                client.Email =
                    email;
            }

            client.UpdatedAt =
                now;
        }


        private static bool IsUniqueViolation(
            DbUpdateException ex)
        {
            return ex.InnerException is SqlException
            {
                Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation
            };
        }


        // =========================================================
        // ORDER EMAIL BODY
        // =========================================================

        private static string BuildOrderEmailBody(
            Client client,
            Dictionary<int, Product> products,
            Order order)
        {
                var itemsHtml = new StringBuilder();

                foreach (var item in order.Items)
                {
                    var product = products[item.ProductId];
                    var subtotal = item.UnitPrice * item.Quantity;

                    var sizeName = item.ProductVariantId.HasValue
                        ? product.Variants
                            .FirstOrDefault(v => v.Id == item.ProductVariantId.Value)?
                            .Size?.Name
                        : null;

                    var variantText = sizeName != null
                        ? $@"<div style='font-size:12px;color:#64748b;margin-top:5px;'>
                        الحجم: {WebUtility.HtmlEncode(sizeName)}
                     </div>"
                        : "";

                    var modifiersText = item.Modifiers.Count > 0
                        ? $@"<div style='font-size:12px;color:#64748b;margin-top:5px;'>
                        إضافات: {WebUtility.HtmlEncode(string.Join("، ", item.Modifiers.Select(m =>
                            m.Quantity > 1 ? $"{m.NameAr} ×{m.Quantity}" : m.NameAr)))}
                     </div>"
                        : "";

                    itemsHtml.Append($@"
                <tr>
                    <td style='padding:14px 10px;border-bottom:1px solid #e2e8f0;'>
                        <strong>{WebUtility.HtmlEncode(product.NameAr)}</strong>
                        {variantText}
                        {modifiersText}
                    </td>
                    <td style='padding:14px 6px;text-align:center;border-bottom:1px solid #e2e8f0;'>
                        {item.Quantity}
                    </td>
                    <td dir='ltr' style='padding:14px 6px;text-align:center;border-bottom:1px solid #e2e8f0;white-space:nowrap;'>
                        {item.UnitPrice:N2}
                    </td>
                    <td dir='ltr' style='padding:14px 10px;text-align:center;border-bottom:1px solid #e2e8f0;font-weight:bold;white-space:nowrap;'>
                        {subtotal:N2}
                    </td>
                </tr>");
                }

                var clientNameHtml =
                    WebUtility.HtmlEncode(client.Name ?? "");

                var clientPhoneHtml =
                    WebUtility.HtmlEncode(client.PhoneNumber ?? "");

                var clientAddressHtml =
                    WebUtility.HtmlEncode(client.Address ?? "");

                var clientEmailHtml =
                    WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(client.Email)
                            ? "غير مسجل"
                            : client.Email);

                var orderNote = string.IsNullOrWhiteSpace(order.Note)
                    ? "لا توجد ملاحظات"
                    : WebUtility.HtmlEncode(order.Note);

                var emailBody = $@"
<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>

<body dir='rtl' style='margin:0;padding:0;background:#f1f5f9;
    font-family:Tahoma,Arial,sans-serif;color:#1e293b;'>

<div style='max-width:760px;margin:30px auto;padding:0 12px;'>

    <!-- HEADER -->
    <div style='background:#172554;padding:30px 24px;
        border-radius:14px 14px 0 0;text-align:center;'>

        <h1 style='color:#ffffff;margin:0;font-size:27px;'>
            إشعار طلب جديد
        </h1>
    </div>

    <!-- CONTENT -->
    <div style='background:#ffffff;padding:26px 20px;'>

        <!-- ORDER SUMMARY -->
        <div style='background:#eff6ff;border:1px solid #dbeafe;
            border-radius:10px;padding:20px;margin-bottom:28px;'>

            <table role='presentation' width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td>
                        <div style='font-size:12px;color:#64748b;'>
                            رقم الطلب
                        </div>

                        <div dir='ltr' style='text-align:right;font-size:25px;
                            font-weight:bold;color:#1d4ed8;margin-top:8px;'>
                            #{order.Id}
                        </div>
                    </td>

                    <td style='text-align:left;vertical-align:top;'>
                        <span style='display:inline-block;background:#fef3c7;
                            color:#92400e;padding:8px 13px;border-radius:20px;
                            font-size:12px;font-weight:bold;'>
                            قيد الانتظار
                        </span>
                    </td>
                </tr>
            </table>
        </div>

        <!-- CLIENT DETAILS -->
        <h2 style='font-size:19px;color:#0f172a;margin:0 0 15px;'>
            بيانات العميل
        </h2>

        <table role='presentation' width='100%' cellpadding='0' cellspacing='0'
            style='border:1px solid #e2e8f0;border-radius:10px;
            margin-bottom:30px;font-size:14px;'>

            <tr>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;
                    width:115px;color:#64748b;'>
                    اسم العميل
                </td>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;font-weight:bold;'>
                    {clientNameHtml}
                </td>
            </tr>

            <tr>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;color:#64748b;'>
                    رقم الهاتف
                </td>
                <td dir='ltr' style='padding:14px;text-align:right;border-bottom:1px solid #e2e8f0;'>
                    {clientPhoneHtml}
                </td>
            </tr>

            <tr>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;color:#64748b;'>
                    البريد الإلكتروني
                </td>
                <td dir='ltr' style='padding:14px;text-align:right;border-bottom:1px solid #e2e8f0;word-break:break-word;'>
                    {clientEmailHtml}
                </td>
            </tr>

            <tr>
                <td style='padding:14px;color:#64748b;'>
                    عنوان العميل
                </td>
                <td style='padding:14px;line-height:1.9;'>
                    {clientAddressHtml}
                </td>
            </tr>
        </table>

        <!-- ORDER ITEMS -->
        <h2 style='font-size:19px;color:#0f172a;margin:0 0 15px;'>
            تفاصيل المنتجات
        </h2>

        <table width='100%' cellpadding='0' cellspacing='0'
            style='border-collapse:collapse;font-size:12px;'>

            <thead>
                <tr style='background:#f8fafc;'>
                    <th style='padding:13px 8px;text-align:right;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        المنتج
                    </th>
                    <th style='padding:13px 5px;text-align:center;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        الكمية
                    </th>
                    <th style='padding:13px 5px;text-align:center;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        سعر الوحدة
                    </th>
                    <th style='padding:13px 5px;text-align:center;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        الإجمالي
                    </th>
                </tr>
            </thead>

            <tbody>
                {itemsHtml}
            </tbody>
        </table>

        <!-- CUSTOMER NOTE -->
        <div style='margin-top:24px;padding:18px;background:#fff7ed;
            border-right:4px solid #f97316;border-radius:6px;'>

            <strong style='color:#9a3412;font-size:15px;'>
                ملاحظات الطلب
            </strong>

            <p style='margin:10px 0 0;color:#334155;font-size:14px;line-height:2;white-space:pre-wrap;'>
                {orderNote}
            </p>
        </div>

        <!-- TOTAL -->
        <div style='margin-top:24px;background:#f8fafc;
            border-radius:10px;padding:22px;'>

            <table role='presentation' width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td style='font-size:15px;color:#475569;'>
                        إجمالي قيمة الطلب
                    </td>

                    <td dir='ltr' style='text-align:left;font-size:23px;
                        font-weight:bold;color:#1d4ed8;white-space:nowrap;'>
                        {order.TotalAmount:N2}
                    </td>
                </tr>
            </table>
        </div>

    </div>

    <!-- FOOTER -->
    <div style='background:#0f172a;padding:18px;
        text-align:center;border-radius:0 0 14px 14px;'>

        <p style='margin:0;color:#94a3b8;font-size:12px;line-height:2;'>
            يُرجى عدم الرد مباشرةً على هذه الرسالة.
        </p>
    </div>

</div>
</body>
</html>";

                return emailBody;
        }



        // =========================================================
        // PRICE ONE ITEM
        // =========================================================
        //
        // Validates the selected size and modifiers and returns
        // the unit price:
        //
        // size price (or product price) after discount
        // + sum(modifier price * modifier quantity)
        //
        // =========================================================

        private static PricedOrderItem PriceItem(
            RequestedOrderItem requestedItem,
            Product product)
        {
            // =================================================
            // PRODUCT AVAILABILITY
            // =================================================

            if (!product.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"المنتج {product.NameAr} غير متاح حالياً.");
            }


            ProductVariant? variant = null;


            // =================================================
            // PRODUCT HAS SIZES
            // =================================================

            if (product.Variants.Count > 0)
            {
                // A product with sizes MUST receive a size.

                if (!requestedItem.ProductVariantId.HasValue)
                {
                    throw new InvalidOperationException(
                        "يجب اختيار الحجم الخاص بالمنتج.");
                }


                variant =
                    product.Variants.FirstOrDefault(
                        v =>
                            v.Id ==
                            requestedItem.ProductVariantId.Value);


                if (variant == null)
                {
                    throw new InvalidOperationException(
                        "اختيار المنتج غير صحيح.");
                }


                if (!variant.IsActive ||
                    !variant.InStock ||
                    (variant.Size != null && !variant.Size.IsActive))
                {
                    throw new InvalidOperationException(
                        "الحجم المختار غير متاح حالياً.");
                }
            }


            // =================================================
            // PRODUCT WITHOUT SIZES
            // =================================================

            else if (requestedItem.ProductVariantId.HasValue)
            {
                // A product without sizes MUST NOT receive
                // a variant ID.

                throw new InvalidOperationException(
                    "اختيار المنتج غير صحيح.");
            }


            // =================================================
            // BASE PRICE (AFTER DISCOUNT)
            // =================================================

            var basePrice =
                ApplyDiscount(
                    variant?.Price ?? product.Price,
                    product.DiscountPercentage);


            // =================================================
            // MODIFIERS
            // =================================================

            var productGroups =
                product.ProductModifierGroups
                    .Where(pg => pg.ModifierGroup.IsActive)
                    .ToList();


            var optionsById =
                productGroups
                    .SelectMany(pg => pg.ModifierGroup.Options
                        .Select(o => (Group: pg, Option: o)))
                    .ToDictionary(x => x.Option.Id);


            var selectedModifiers =
                new List<(ModifierOption Option, int Quantity)>();


            var selectedPerGroup =
                new Dictionary<int, int>();


            foreach (var requestedModifier in requestedItem.Modifiers)
            {
                if (!optionsById.TryGetValue(
                        requestedModifier.ModifierOptionId,
                        out var match))
                {
                    throw new InvalidOperationException(
                        $"إضافة غير صحيحة للمنتج {product.NameAr}.");
                }


                if (!match.Option.IsAvailable)
                {
                    throw new InvalidOperationException(
                        $"الإضافة {match.Option.NameAr} غير متاحة حالياً.");
                }


                selectedModifiers.Add(
                    (match.Option, requestedModifier.Quantity));


                selectedPerGroup[match.Group.ModifierGroupId] =
                    selectedPerGroup.GetValueOrDefault(
                        match.Group.ModifierGroupId) +
                    requestedModifier.Quantity;
            }


            // =================================================
            // GROUP SELECTION RULES (required / min / max)
            // =================================================

            foreach (var group in productGroups)
            {
                var selected =
                    selectedPerGroup.GetValueOrDefault(
                        group.ModifierGroupId);


                var min =
                    group.IsRequired
                        ? Math.Max(1, group.MinSelections)
                        : group.MinSelections;


                if (selected < min)
                {
                    throw new InvalidOperationException(
                        $"يجب اختيار {group.ModifierGroup.NameAr} للمنتج {product.NameAr}.");
                }


                if (selected > group.MaxSelections)
                {
                    throw new InvalidOperationException(
                        $"الحد الأقصى لاختيار {group.ModifierGroup.NameAr} هو {group.MaxSelections}.");
                }
            }


            var modifiersPrice =
                selectedModifiers.Sum(
                    m => m.Option.Price * m.Quantity);


            return new PricedOrderItem(
                requestedItem,
                product,
                variant,
                selectedModifiers,
                Math.Round(
                    basePrice + modifiersPrice,
                    2,
                    MidpointRounding.AwayFromZero));
        }


        // =========================================================
        // MODIFIER HELPERS
        // =========================================================

        private static List<RequestedModifier> NormalizeModifiers(
            List<CreateOrderItemModifierDto>? modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return [];
            }


            if (modifiers.Any(m => m.ModifierOptionId <= 0))
            {
                throw new InvalidOperationException(
                    "إضافة غير صحيحة.");
            }


            if (modifiers.Any(m => m.Quantity <= 0))
            {
                throw new InvalidOperationException(
                    "كمية الإضافة يجب ان تكون اكبر من الصفر.");
            }


            // Same option sent twice -> one entry with summed quantity.
            return modifiers
                .GroupBy(m => m.ModifierOptionId)
                .Select(g => new RequestedModifier(
                    g.Key,
                    g.Sum(m => m.Quantity)))
                .OrderBy(m => m.ModifierOptionId)
                .ToList();
        }


        private static string BuildModifiersKey(
            List<RequestedModifier> modifiers)
        {
            return string.Join(
                ",",
                modifiers.Select(m => $"{m.ModifierOptionId}x{m.Quantity}"));
        }


        // =========================================================
        // APPLY DISCOUNT
        // =========================================================

        private static decimal ApplyDiscount(
            decimal price,
            decimal discountPercentage)
        {
            if (discountPercentage > 0)
            {
                price -=
                    price *
                    discountPercentage /
                    100;
            }


            return Math.Round(
                price,
                2,
                MidpointRounding.AwayFromZero);
        }


        // =========================================================
        // GET ORDERS
        // =========================================================

        public async Task<PagedResponse<OrderDto>> GetOrders(
            int page = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            page =
                Math.Max(
                    page,
                    1);


            pageSize =
                Math.Clamp(
                    pageSize,
                    1,
                    100);


            var items =
                await _context.Orders
                    .AsNoTracking()
                    .OrderByDescending(
                        o => o.OrderDate)
                    .ThenByDescending(
                        o => o.Id)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(
                        pageSize + 1)
                    .Select(
                        OrderProjection())
                    .ToListAsync(
                        cancellationToken);


            var hasMore =
                items.Count >
                pageSize;


            if (hasMore)
            {
                items.RemoveAt(
                    items.Count - 1);
            }


            return new PagedResponse<OrderDto>
            {
                Items =
                    items,

                Page =
                    page,

                PageSize =
                    pageSize,

                HasMore =
                    hasMore
            };
        }


        // =========================================================
        // GET ORDER BY ID
        // =========================================================

        public async Task<OrderDto?> GetOrder(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return null;
            }


            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(OrderProjection())
                .FirstOrDefaultAsync(
                    cancellationToken);
        }


        // =========================================================
        // GET ORDERS BY CLIENT
        // =========================================================

        public async Task<List<OrderDto>> GetOrdersByClient(
            int clientId,
            CancellationToken cancellationToken = default)
        {
            if (clientId <= 0)
            {
                return [];
            }


            return await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.ClientId ==
                    clientId)
                .OrderByDescending(
                    o => o.OrderDate)
                .ThenByDescending(
                    o => o.Id)
                .Select(
                    OrderProjection())
                .ToListAsync(
                    cancellationToken);
        }


        // =========================================================
        // UPDATE ORDER STATUS
        // =========================================================

        public async Task<bool> UpdateOrderStatus(
            int id,
            string status,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return false;
            }


            if (string.IsNullOrWhiteSpace(status))
            {
                throw new InvalidOperationException(
                    "حاله الطلب غير متوفره.");
            }


            if (!Enum.TryParse<OrderStatus>(
                    status.Trim(),
                    true,
                    out var orderStatus))
            {
                throw new InvalidOperationException(
                    "حاله الطلب ليست مدعومه.");
            }


            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(
                        o => o.Id == id,
                        cancellationToken);


            if (order == null)
            {
                return false;
            }


            // =================================================
            // NOTHING TO UPDATE
            // =================================================

            if (order.Status ==
                orderStatus)
            {
                return true;
            }


            // =================================================
            // CANCELLED ORDER IS FINAL
            // =================================================

            if (order.Status ==
                OrderStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "لا يمكن تغيير حالة طلب ملغي.");
            }


            order.Status =
                orderStatus;


            await _context.SaveChangesAsync(
                cancellationToken);


            return true;
        }


        // =========================================================
        // CANCEL ORDER
        // =========================================================
        //
        // IMPORTANT:
        // This method ONLY changes the order status.
        //
        // It NEVER changes Product.StockQuantity.
        //
        // It NEVER changes ProductVariant.StockQuantity.
        //
        // =========================================================

        public async Task<bool> CancelOrder(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return false;
            }


            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(
                        o => o.Id == id,
                        cancellationToken);


            if (order == null)
            {
                return false;
            }


            // =================================================
            // ALREADY CANCELLED
            // =================================================

            if (order.Status ==
                OrderStatus.Cancelled)
            {
                return true;
            }


            // =================================================
            // CANCEL ORDER
            // =================================================

            order.Status =
                OrderStatus.Cancelled;


            // =================================================
            // SAVE
            // =================================================

            await _context.SaveChangesAsync(
                cancellationToken);


            return true;
        }

        public async Task<bool> CompleteOrder(
           int id,
           CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return false;
            }


            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(
                        o => o.Id == id,
                        cancellationToken);


            if (order == null)
            {
                return false;
            }


            // =================================================
            // ALREADY COMPLETED
            // =================================================

            if (order.Status ==
                OrderStatus.Confirmed)
            {
                return true;
            }


            // =================================================
            // COMPLETE ORDER
            // =================================================

            order.Status =
                OrderStatus.Confirmed;

            // =================================================
            // SAVE
            // =================================================

            await _context.SaveChangesAsync(
                cancellationToken);


            return true;
        }

        // =========================================================
        // CURRENT MONTH DASHBOARD
        // =========================================================

        public async Task<DashboardStatsDto> GetCurrentMonthStats(
            CancellationToken cancellationToken = default)
        {
            var now =
                DateTime.UtcNow;


            var startDate =
                new DateTime(
                    now.Year,
                    now.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);


            var endDate =
                startDate.AddMonths(1);


            return await GetStats(
                _context.Orders
                    .AsNoTracking()
                    .Where(o =>
                        o.Status !=
                            OrderStatus.Cancelled &&
                        o.OrderDate >=
                            startDate &&
                        o.OrderDate <
                            endDate),
                cancellationToken);
        }


        // =========================================================
        // TOTAL DASHBOARD
        // =========================================================

        public async Task<DashboardStatsDto> GetTotalStats(
            CancellationToken cancellationToken = default)
        {
            return await GetStats(
                _context.Orders
                    .AsNoTracking()
                    .Where(o =>
                        o.Status !=
                            OrderStatus.Cancelled),
                cancellationToken);
        }


        // =========================================================
        // DASHBOARD AGGREGATES
        // =========================================================
        //
        // Plain COUNT / SUM queries. Each one translates to a
        // single SQL aggregate, with no grouping or SelectMany.
        //
        // =========================================================

        private static async Task<DashboardStatsDto> GetStats(
            IQueryable<Order> orders,
            CancellationToken cancellationToken)
        {
            var count =
                await orders.CountAsync(
                    cancellationToken);

            if (count == 0)
            {
                return new DashboardStatsDto
                {
                    Orders = 0,
                    Sales = 0,
                    Gain = 0
                };
            }

            var sales =
                await orders.SumAsync(
                    o => o.TotalAmount,
                    cancellationToken);

            var gain =
                await orders
                    .SelectMany(o => o.Items)
                    .SumAsync(
                        i =>
                            (i.UnitPrice -
                             i.ActualPrice) *
                            i.Quantity,
                        cancellationToken);

            return new DashboardStatsDto
            {
                Orders =
                    count,

                Sales =
                    Math.Round(
                        sales,
                        2,
                        MidpointRounding.AwayFromZero),

                Gain =
                    Math.Round(
                        gain,
                        2,
                        MidpointRounding.AwayFromZero)
            };
        }


        // =========================================================
        // ORDER PROJECTION
        // =========================================================
        //
        // Projection instead of Include().
        //
        // Only required fields are selected.
        //
        // Only the first image is returned.
        //
        // Only the selected variant is returned.
        //
        // =========================================================

        private static Expression<Func<Order, OrderDto>>
            OrderProjection()
        {
            return o => new OrderDto
            {
                Id =
                    o.Id,

                ClientId =
                    o.ClientId,

                ClientName =
                    o.Client.Name,

                PhoneNumber =
                    o.Client.PhoneNumber,
                Note=o.Note,

                Email =
                    o.Client.Email,

                Address =
                    o.Client.Address,

                OrderDate =
                    o.OrderDate,

                TotalAmount =
                    o.TotalAmount,

                Status =
                    o.Status.ToString(),

                Items =
                    o.Items
                        .Select(i => new OrderItemDto
                        {
                            Id =
                                i.Id,

                            ProductId =
                                i.ProductId,

                            ProductName =
                                i.Product.NameEn,

                            ProductNameAr =
                                i.Product.NameAr,

                            // =================================================
                            // FIRST PRODUCT IMAGE
                            // =================================================

                            ImageUrl =
                                i.Product.Images
                                    .OrderBy(image =>
                                        image.SortOrder)
                                    .Select(image =>
                                        image.ImageUrl)
                                    .FirstOrDefault(),

                            // =================================================
                            // VARIANT
                            // =================================================

                            ProductVariantId =
                                i.ProductVariantId,

                            SizeName =
                                i.ProductVariant != null &&
                                i.ProductVariant.Size != null
                                    ? i.ProductVariant.Size.Name
                                    : null,

                            // =================================================
                            // MODIFIERS (snapshot taken at order time)
                            // =================================================

                            Modifiers =
                                i.Modifiers
                                    .OrderBy(m => m.Id)
                                    .Select(m => new OrderItemModifierDto
                                    {
                                        ModifierOptionId =
                                            m.ModifierOptionId,

                                        NameEn =
                                            m.NameEn,

                                        NameAr =
                                            m.NameAr,

                                        UnitPrice =
                                            m.UnitPrice,

                                        Quantity =
                                            m.Quantity
                                    })
                                    .ToList(),

                            // =================================================
                            // QUANTITY
                            // =================================================

                            Quantity =
                                i.Quantity,

                            // =================================================
                            // PRICE
                            // =================================================

                            UnitPrice =
                                i.UnitPrice,

                            ActualPrice =
                                i.ActualPrice,

                            TotalPrice =
                                i.UnitPrice *
                                i.Quantity
                        })
                        .ToList()
            };
        }


        // =========================================================
        // INTERNAL REQUESTED ITEM
        // =========================================================

        private sealed class RequestedOrderItem
        {
            public int ProductId { get; init; }

            public int? ProductVariantId { get; init; }

            public int Quantity { get; init; }

            public List<RequestedModifier> Modifiers { get; init; } = [];

            public decimal? DisplayedUnitPrice { get; set; }
        }


        private sealed record RequestedModifier(
            int ModifierOptionId,
            int Quantity);


        private sealed record PricedOrderItem(
            RequestedOrderItem Request,
            Product Product,
            ProductVariant? Variant,
            List<(ModifierOption Option, int Quantity)> Modifiers,
            decimal UnitPrice);
    }
}
