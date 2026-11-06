namespace ShopService.Contracts;

// Kontrakty zdarzeń publikowanych przez ShopService (RabbitMQ) – bez zależności, do użycia przez inne serwisy.
// Zmiany tylko zgodne wstecz: nowe pola opcjonalne, bez zmiany nazw i typów istniejących.

public interface IIntegrationEvent
{
    static abstract string EventName { get; }

    DateTimeOffset OccurredAt { get; }
}

public static class ShopEvents
{
    /// <summary>Exchange typu topic; routing key = <see cref="IIntegrationEvent.EventName"/>.</summary>
    public const string Exchange = "shop.events";
}

public sealed record OrderPlaced(
    Guid OrderId,
    Guid UserId,
    string CustomerEmail,
    decimal Total,
    string Currency,
    int ItemCount,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventName => "order.placed";
}

/// <summary>Statusy jako tekst: <c>New</c>, <c>Paid</c>, <c>Shipped</c>, <c>Delivered</c>, <c>Cancelled</c>.</summary>
public sealed record OrderStatusChanged(
    Guid OrderId,
    Guid UserId,
    string CustomerEmail,
    string PreviousStatus,
    string NewStatus,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventName => "order.status_changed";
}
