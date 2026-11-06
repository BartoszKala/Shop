using ShopService.Domain;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;
using ShopService.Domain.Events;

namespace ShopService.UnitTests.Domain;

public sealed class SlugTests
{
    [Theory]
    [InlineData("Dom i ogród", "dom-i-ogrod")]
    [InlineData("  Łóżka & Żyrandole!  ", "lozka-zyrandole")]
    [InlineData("USB-C 65 W", "usb-c-65-w")]
    public void From_builds_ascii_slug(string text, string expected)
    {
        Assert.Equal(expected, Slug.From(text));
    }

    [Theory]
    [InlineData("ksiazki", true)]
    [InlineData("dom-i-ogrod", true)]
    [InlineData("Ksiazki", false)]
    [InlineData("dom--ogrod", false)]
    [InlineData("-sport", false)]
    [InlineData("", false)]
    public void IsValid_accepts_only_normalized_slugs(string slug, bool expected)
    {
        Assert.Equal(expected, Slug.IsValid(slug));
    }
}

public sealed class CategoryTests
{
    [Fact]
    public void Create_generates_slug_from_name_when_missing()
    {
        var result = Category.Create(" Dom i ogród ", null, "  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Dom i ogród", result.Value.Name);
        Assert.Equal("dom-i-ogrod", result.Value.Slug);
        Assert.Null(result.Value.Description);
    }

    [Fact]
    public void Create_rejects_invalid_slug()
    {
        var result = Category.Create("Sport", "Sport Zimowy", null);

        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.Details!.ContainsKey(nameof(Category.Slug)));
    }
}

public sealed class MoneyTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.005)]
    public void Create_rejects_negative_or_sub_grosz_amounts(decimal amount)
    {
        var result = Money.Create(amount, "Price");

        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.Details!.ContainsKey("Price"));
    }

    [Fact]
    public void Arithmetic_and_sum_keep_exact_amounts()
    {
        var total = new[] { Money.From(59.90m) * 2, Money.From(19.99m) * 3 }.Sum();

        Assert.Equal(Money.From(179.77m), total);
        Assert.True(total > Money.Zero);
        Assert.Equal("179.77 PLN", total.ToString());
    }

    [Fact]
    public void From_throws_for_invalid_trusted_value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.From(-1m));
    }
}

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000.01)]
    public void Create_rejects_price_out_of_range(decimal price)
    {
        var result = Product.Create(CategoryId.New(), "Bidon", null, Money.From(price), 1, null, true, Now);

        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.Details!.ContainsKey(nameof(Product.Price)));
    }

    [Fact]
    public void RemoveFromStock_fails_when_not_enough_items()
    {
        var product = Product.Create(CategoryId.New(), "Bidon", null, Money.From(19.99m), 2, null, true, Now).Value;

        var result = product.RemoveFromStock(3, Now);

        Assert.Equal("product.insufficient_stock", result.Error!.Code);
        Assert.Equal(2, product.Stock);
    }
}

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly ShippingAddress Address = new("Jan Kowalski", "Długa 1", "00-001", "Warszawa", "+48 600 000 000");
    private static readonly UserId Customer = UserId.From(Guid.NewGuid());

    private static Product NewProduct(decimal price, int stock, bool isActive = true) =>
        Product.Create(CategoryId.New(), "Produkt", null, Money.From(price), stock, null, isActive, Now).Value;

    private static Result<Order> Place(IReadOnlyList<OrderLine> lines, string? idempotencyKey = null) =>
        Order.Place(Customer, "jan@shop.local", Address, lines, idempotencyKey, Now);

    private static Order NewOrder()
    {
        var order = Place([new OrderLine(NewProduct(10m, 5), 1)]).Value;
        order.ClearDomainEvents();
        return order;
    }

    [Fact]
    public void Place_copies_prices_computes_total_and_takes_stock()
    {
        var mat = NewProduct(59.90m, 10);
        var bottle = NewProduct(19.99m, 3);

        var result = Place([new OrderLine(mat, 2), new OrderLine(bottle, 3)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.New, result.Value.Status);
        Assert.Equal(Money.From(2 * 59.90m + 3 * 19.99m), result.Value.Total);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(8, mat.Stock);
        Assert.Equal(0, bottle.Stock);
    }

    [Fact]
    public void Place_raises_order_placed_event()
    {
        var order = Place([new OrderLine(NewProduct(10m, 5), 3)]).Value;

        var placed = Assert.IsType<OrderPlaced>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id, placed.OrderId);
        Assert.Equal(Customer, placed.UserId);
        Assert.Equal(Money.From(30m), placed.Total);
        Assert.Equal(3, placed.ItemCount);
    }

    [Fact]
    public void Place_does_not_touch_stock_when_any_line_fails()
    {
        var available = NewProduct(10m, 5);
        var scarce = NewProduct(10m, 1);

        var result = Place([new OrderLine(available, 2), new OrderLine(scarce, 2)]);

        Assert.Equal("order.insufficient_stock", result.Error!.Code);
        Assert.Equal(5, available.Stock);
        Assert.Equal(1, scarce.Stock);
    }

    [Fact]
    public void Place_rejects_inactive_product()
    {
        var result = Place([new OrderLine(NewProduct(10m, 5, isActive: false), 1)]);

        Assert.Equal("order.product_unavailable", result.Error!.Code);
    }

    [Fact]
    public void Place_rejects_empty_order()
    {
        var result = Place([]);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")]
    public void Place_rejects_invalid_idempotency_key(string key)
    {
        var result = Place([new OrderLine(NewProduct(10m, 5), 1)], key);

        Assert.True(result.Error!.Details!.ContainsKey("Idempotency-Key"));
    }

    [Fact]
    public void HasSameLines_compares_products_and_quantities()
    {
        var product = NewProduct(10m, 5);
        var order = Place([new OrderLine(product, 2)], "key-1").Value;

        Assert.True(order.HasSameLines(new Dictionary<ProductId, int> { [product.Id] = 2 }));
        Assert.False(order.HasSameLines(new Dictionary<ProductId, int> { [product.Id] = 3 }));
        Assert.False(order.HasSameLines(new Dictionary<ProductId, int> { [product.Id] = 2, [ProductId.New()] = 1 }));
    }

    [Fact]
    public void Full_lifecycle_ends_in_delivered_with_no_further_transitions()
    {
        var order = NewOrder();

        Assert.True(order.MarkAsPaid(Now).IsSuccess);
        Assert.True(order.Ship(Now).IsSuccess);
        Assert.True(order.MarkAsDelivered(Now.AddDays(2)).IsSuccess);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(Now.AddDays(2), order.UpdatedAt);
        Assert.DoesNotContain(Enum.GetValues<OrderStatus>(), order.CanChangeStatusTo);
    }

    [Fact]
    public void Status_change_raises_event_with_previous_and_new_status()
    {
        var order = NewOrder();

        order.MarkAsPaid(Now);

        var changed = Assert.IsType<OrderStatusChanged>(Assert.Single(order.DomainEvents));
        Assert.Equal(OrderStatus.New, changed.PreviousStatus);
        Assert.Equal(OrderStatus.Paid, changed.NewStatus);
    }

    [Fact]
    public void Cancel_raises_event_with_lines_to_return_to_stock()
    {
        var order = NewOrder();

        Assert.True(order.Cancel(Now).IsSuccess);

        var cancelled = Assert.Single(order.DomainEvents.OfType<OrderCancelled>());
        var line = Assert.Single(cancelled.Lines);
        Assert.Equal(order.Items[0].ProductId, line.ProductId);
        Assert.Equal(1, line.Quantity);
        Assert.Single(order.DomainEvents.OfType<OrderStatusChanged>());
    }

    [Fact]
    public void Order_cannot_be_shipped_before_payment()
    {
        var order = NewOrder();

        var result = order.Ship(Now);

        Assert.Equal("order.invalid_status_transition", result.Error!.Code);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void Shipped_order_cannot_be_cancelled()
    {
        var order = NewOrder();
        order.MarkAsPaid(Now);
        order.Ship(Now);
        order.ClearDomainEvents();

        var result = order.Cancel(Now);

        Assert.Equal("order.invalid_status_transition", result.Error!.Code);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Empty(order.DomainEvents);
    }
}

public sealed class StronglyTypedIdTests
{
    [Fact]
    public void Ids_compare_by_value()
    {
        var guid = Guid.CreateVersion7();

        Assert.Equal(ProductId.From(guid), ProductId.From(guid));
        Assert.Equal(guid.ToString(), ProductId.From(guid).ToString());
    }

    [Fact]
    public void TypeConverter_round_trips_string()
    {
        var id = OrderId.New();
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(OrderId));

        Assert.Equal(id, converter.ConvertFromInvariantString(id.ToString()));
    }
}
