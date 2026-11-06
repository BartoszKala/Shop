using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopService.Infrastructure.Persistence;

namespace ShopService.Infrastructure.Outbox;

/// <summary>
/// Wysyła zdarzenia z outboxu do RabbitMQ z ponawianiem (backoff do 1 h). Kilka instancji serwisu może działać
/// równolegle – każda blokuje swoje wiadomości (<c>FOR UPDATE SKIP LOCKED</c>).
/// </summary>
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
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IIntegrationEventSender>();
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
                await DeliverAsync(sender, message, settings, ct);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return messages.Count;
        });
    }

    private async Task DeliverAsync(IIntegrationEventSender sender, OutboxMessage message, OutboxOptions settings, CancellationToken ct)
    {
        try
        {
            await sender.SendAsync(message, ct);
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
                logger.LogError(ex, "Outbox message {MessageId} ({Name}) failed permanently after {Attempts} attempts",
                    message.Id, message.Name, message.Attempts);
            }
            else
            {
                logger.LogWarning(ex, "Outbox message {MessageId} ({Name}) failed, retrying in {Delay}",
                    message.Id, message.Name, delay);
            }
        }
    }
}
