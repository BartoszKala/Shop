using ShopService.Domain.Common;
using ShopService.Domain.Entities;

namespace ShopService.Domain.Events;

public sealed record OrderPlaced(
    OrderId OrderId,
    UserId UserId,
    string CustomerEmail,
    Money Total,
    int ItemCount,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderStatusChanged(
    OrderId OrderId,
    UserId UserId,
    string CustomerEmail,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Zgłaszane razem z <see cref="OrderStatusChanged"/> – niesie pozycje, które trzeba zwrócić do magazynu.</summary>
public sealed record OrderCancelled(
    OrderId OrderId,
    IReadOnlyList<OrderCancelled.Line> Lines,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public sealed record Line(ProductId ProductId, int Quantity);
}
