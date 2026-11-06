using ShopService.Domain.Common;
using ShopService.Domain.Events;

namespace ShopService.Domain.Entities;

public enum OrderStatus
{
    New,
    Paid,
    Shipped,
    Delivered,
    Cancelled
}

public sealed record ShippingAddress(string Recipient, string Street, string PostalCode, string City, string Phone)
{
    public const int RecipientMaxLength = 200;
    public const int StreetMaxLength = 200;
    public const int PostalCodeMaxLength = 10;
    public const int CityMaxLength = 100;
    public const int PhoneMaxLength = 20;
}

public sealed record OrderLine(Product Product, int Quantity);

public class Order : AggregateRoot
{
    public const int MaxLines = 50;
    public const int MaxQuantityPerLine = 99;
    public const int EmailMaxLength = 256;
    public const int IdempotencyKeyMaxLength = 64;

    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.New] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public OrderId Id { get; private set; }

    /// <summary>Id użytkownika z IdentityService (claim <c>sub</c>).</summary>
    public UserId UserId { get; private set; }

    public string CustomerEmail { get; private set; } = string.Empty;
    public ShippingAddress ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; }

    /// <summary>Klucz z nagłówka <c>Idempotency-Key</c> – ponowne wysłanie tego samego zamówienia nie tworzy drugiego.</summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>Tworzy zamówienie i zdejmuje towar z magazynu. Ceny i nazwy są kopiowane z produktów w chwili zakupu.</summary>
    public static Result<Order> Place(
        UserId userId,
        string customerEmail,
        ShippingAddress address,
        IReadOnlyList<OrderLine> lines,
        string? idempotencyKey,
        DateTimeOffset now)
    {
        if (lines.Count == 0)
        {
            return Error.Validation("Items", "Zamówienie musi zawierać co najmniej jeden produkt.");
        }
        if (lines.Count > MaxLines)
        {
            return Error.Validation("Items", $"Zamówienie może zawierać najwyżej {MaxLines} pozycji.");
        }
        if (lines.DistinctBy(l => l.Product.Id).Count() != lines.Count)
        {
            return Error.Validation("Items", "Każdy produkt może wystąpić w zamówieniu tylko raz.");
        }
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Error.Validation(nameof(CustomerEmail), "Brak adresu e-mail klienta.");
        }
        if (idempotencyKey is { Length: 0 or > IdempotencyKeyMaxLength })
        {
            return Error.Validation("Idempotency-Key", $"Klucz idempotencji musi mieć od 1 do {IdempotencyKeyMaxLength} znaków.");
        }

        var order = new Order
        {
            Id = OrderId.New(),
            UserId = userId,
            CustomerEmail = customerEmail.Trim(),
            ShippingAddress = address,
            Status = OrderStatus.New,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Najpierw sprawdzamy wszystkie pozycje, żeby nieudane zamówienie nie zmieniło stanów magazynowych
        foreach (var (product, quantity) in lines)
        {
            if (quantity is < 1 or > MaxQuantityPerLine)
            {
                return Error.Validation("Quantity", $"Ilość musi mieścić się w zakresie 1–{MaxQuantityPerLine}.");
            }
            if (!product.IsActive)
            {
                return Error.Conflict("order.product_unavailable", $"Produkt „{product.Name}” nie jest już dostępny.");
            }
            if (quantity > product.Stock)
            {
                return Error.Conflict("order.insufficient_stock",
                    $"Niewystarczająca ilość produktu „{product.Name}” w magazynie (dostępne: {product.Stock}).");
            }
        }

        foreach (var (product, quantity) in lines)
        {
            product.RemoveFromStock(quantity, now);
            order._items.Add(OrderItem.Create(order.Id, product, quantity));
        }

        order.Total = order._items.Select(i => i.LineTotal).Sum();
        order.Raise(new OrderPlaced(order.Id, order.UserId, order.CustomerEmail, order.Total, order._items.Sum(i => i.Quantity), now));
        return order;
    }

    /// <summary>Czy zamówienie z tym samym kluczem idempotencji ma te same pozycje (inaczej klucz użyto ponownie do innego koszyka).</summary>
    public bool HasSameLines(IReadOnlyDictionary<ProductId, int> quantities) =>
        _items.Count == quantities.Count
        && _items.All(i => quantities.TryGetValue(i.ProductId, out var quantity) && quantity == i.Quantity);

    public bool CanChangeStatusTo(OrderStatus status) => AllowedTransitions[Status].Contains(status);

    public Result MarkAsPaid(DateTimeOffset now) => TransitionTo(OrderStatus.Paid, now);

    public Result Ship(DateTimeOffset now) => TransitionTo(OrderStatus.Shipped, now);

    public Result MarkAsDelivered(DateTimeOffset now) => TransitionTo(OrderStatus.Delivered, now);

    /// <summary>Anulowanie zwraca towar do magazynu (handler zdarzenia <see cref="OrderCancelled"/>).</summary>
    public Result Cancel(DateTimeOffset now)
    {
        var result = TransitionTo(OrderStatus.Cancelled, now);
        if (result.IsSuccess)
        {
            Raise(new OrderCancelled(Id, _items.Select(i => new OrderCancelled.Line(i.ProductId, i.Quantity)).ToList(), now));
        }

        return result;
    }

    private Result TransitionTo(OrderStatus status, DateTimeOffset now)
    {
        if (!CanChangeStatusTo(status))
        {
            return Error.Conflict("order.invalid_status_transition", $"Nie można zmienić statusu zamówienia z {Status} na {status}.");
        }

        var previous = Status;
        Status = status;
        UpdatedAt = now;
        Raise(new OrderStatusChanged(Id, UserId, CustomerEmail, previous, status, now));
        return Result.Success();
    }
}

public class OrderItem
{
    private OrderItem()
    {
    }

    public OrderItemId Id { get; private set; }
    public OrderId OrderId { get; private set; }

    public ProductId ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public Money LineTotal => UnitPrice * Quantity;

    internal static OrderItem Create(OrderId orderId, Product product, int quantity) => new()
    {
        Id = OrderItemId.New(),
        OrderId = orderId,
        ProductId = product.Id,
        ProductName = product.Name,
        UnitPrice = product.Price,
        Quantity = quantity
    };
}
