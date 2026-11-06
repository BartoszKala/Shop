using System.Text.Json;
using ShopService.Application.Abstractions;
using ShopService.Contracts;
using ShopService.Infrastructure.Persistence;

namespace ShopService.Infrastructure.Outbox;

/// <summary>Dodaje wiadomość do kontekstu bez zapisu – zapisze ją Unit of Work razem ze zmianą, która wywołała zdarzenie.</summary>
internal sealed class OutboxIntegrationEventPublisher(ShopDbContext db, TimeProvider timeProvider) : IIntegrationEventPublisher
{
    public void Publish<TEvent>(TEvent integrationEvent) where TEvent : IIntegrationEvent =>
        db.OutboxMessages.Add(new OutboxMessage
        {
            Name = TEvent.EventName,
            Payload = JsonSerializer.Serialize(integrationEvent, JsonSerializerOptions.Web),
            OccurredAt = integrationEvent.OccurredAt,
            NextAttemptAt = timeProvider.GetUtcNow()
        });
}
