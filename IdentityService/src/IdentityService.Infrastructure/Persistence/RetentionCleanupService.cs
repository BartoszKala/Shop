using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Persistence;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    [Range(1, 3650)]
    public int RefreshTokenDays { get; set; } = 30;

    [Range(1, 3650)]
    public int SecurityEventDays { get; set; } = 365;

    [Range(1, 3650)]
    public int ProcessedOutboxDays { get; set; } = 7;

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
}

internal sealed class RetentionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<RetentionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Retention cleanup failed");
            }
        }
    }

    internal async Task CleanupAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();

        var tokenCutoff = now.AddDays(-settings.RefreshTokenDays);
        var tokens = await db.RefreshTokens
            .Where(t => t.ExpiresAt < tokenCutoff || t.RevokedAt < tokenCutoff)
            .ExecuteDeleteAsync(ct);

        var eventCutoff = now.AddDays(-settings.SecurityEventDays);
        var events = await db.SecurityEvents.Where(e => e.OccurredAt < eventCutoff).ExecuteDeleteAsync(ct);

        var outboxCutoff = now.AddDays(-settings.ProcessedOutboxDays);
        var outbox = await db.OutboxMessages.Where(m => m.ProcessedAt < outboxCutoff).ExecuteDeleteAsync(ct);

        if (tokens + events + outbox > 0)
        {
            logger.LogInformation(
                "Retention cleanup removed {Tokens} refresh tokens, {Events} security events, {Outbox} outbox messages",
                tokens, events, outbox);
        }
    }
}
