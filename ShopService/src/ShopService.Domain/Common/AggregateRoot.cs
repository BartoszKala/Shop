namespace ShopService.Domain.Common;

/// <summary>Fakt biznesowy, który już zaszedł (czas przeszły w nazwie: OrderPlaced, OrderStatusChanged).</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Korzeń agregatu zbiera zdarzenia domenowe; Unit of Work rozsyła je do handlerów tuż przed zapisem,
/// więc skutki zdarzeń (np. zwrot towaru, wpis do outboxu) trafiają do tej samej transakcji.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
