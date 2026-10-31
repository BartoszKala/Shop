using System.Text.Json;
using IdentityService.Infrastructure.Email;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Outbox;

internal sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollingInterval, timeProvider);
        do
        {
            try
            {
                while (await ProcessBatchAsync(stoppingToken) == options.Value.BatchSize)
                {
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox processing failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var settings = options.Value;

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var now = timeProvider.GetUtcNow();
            var messages = await db.OutboxMessages
                .FromSql($"""
                          SELECT * FROM "OutboxMessages"
                          WHERE "ProcessedAt" IS NULL AND "NextAttemptAt" <= {now} AND "Attempts" < {settings.MaxAttempts}
                          ORDER BY "OccurredAt"
                          LIMIT {settings.BatchSize}
                          FOR UPDATE SKIP LOCKED
                          """)
                .ToListAsync(ct);

            foreach (var message in messages)
            {
                await DeliverAsync(scope.ServiceProvider, message, settings, ct);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return messages.Count;
        });
    }

    private async Task DeliverAsync(IServiceProvider services, OutboxMessage message, OutboxOptions settings, CancellationToken ct)
    {
        try
        {
            switch (message.Type)
            {
                case OutboxMessage.EmailType:
                    var email = JsonSerializer.Deserialize<EmailMessage>(message.Payload, JsonSerializerOptions.Web)!;
                    await services.GetRequiredService<IEmailDelivery>().DeliverAsync(email, ct);
                    break;
                case OutboxMessage.IntegrationEventType:
                    await services.GetRequiredService<IIntegrationEventSender>().SendAsync(message, ct);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown outbox message type '{message.Type}'.");
            }

            message.ProcessedAt = timeProvider.GetUtcNow();
            message.LastError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            message.Attempts++;
            message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            var delay = TimeSpan.FromSeconds(Math.Min(10 * Math.Pow(2, message.Attempts - 1), 3600));
            message.NextAttemptAt = timeProvider.GetUtcNow().Add(delay);

            if (message.Attempts >= settings.MaxAttempts)
            {
                logger.LogError(ex, "Outbox message {MessageId} ({Type} {Name}) failed permanently after {Attempts} attempts",
                    message.Id, message.Type, message.Name, message.Attempts);
            }
            else
            {
                logger.LogWarning(ex, "Outbox message {MessageId} ({Type} {Name}) failed, retrying in {Delay}",
                    message.Id, message.Type, message.Name, delay);
            }
        }
    }
}
