using System.Text.Json;
using IdentityService.Application.Abstractions;
using IdentityService.Contracts;
using IdentityService.Infrastructure.Email;
using IdentityService.Infrastructure.Persistence;

namespace IdentityService.Infrastructure.Outbox;

internal sealed class OutboxEmailService(AppIdentityDbContext db, TimeProvider timeProvider) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxMessage.EmailType,
            Payload = JsonSerializer.Serialize(new EmailMessage(to, subject, htmlBody), JsonSerializerOptions.Web),
            OccurredAt = now,
            NextAttemptAt = now
        });
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class OutboxIntegrationEventPublisher(AppIdentityDbContext db, TimeProvider timeProvider) : IIntegrationEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxMessage.IntegrationEventType,
            Name = TEvent.EventName,
            Payload = JsonSerializer.Serialize(integrationEvent, JsonSerializerOptions.Web),
            OccurredAt = integrationEvent.OccurredAt,
            NextAttemptAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
    }
}
