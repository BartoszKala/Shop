using System.Text;
using IdentityService.Contracts;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace IdentityService.Infrastructure.Outbox;

public interface IIntegrationEventSender
{
    Task SendAsync(OutboxMessage message, CancellationToken ct = default);
}

internal sealed class RabbitMqIntegrationEventSender(IConnection connection) : IIntegrationEventSender, IAsyncDisposable
{
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IChannel? _channel;

    public async Task SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var channel = await GetChannelAsync(ct);
        var properties = new BasicProperties
        {
            MessageId = message.Id.ToString(),
            Type = message.Name,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            AppId = "identity-service"
        };

        await channel.BasicPublishAsync(
            IdentityEvents.Exchange, message.Name!, mandatory: false, properties, Encoding.UTF8.GetBytes(message.Payload), ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
        _channelLock.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _channelLock.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            _channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                ct);
            await _channel.ExchangeDeclareAsync(IdentityEvents.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }
}

internal sealed class LoggingIntegrationEventSender(ILogger<LoggingIntegrationEventSender> logger) : IIntegrationEventSender
{
    public Task SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        logger.LogWarning("Messaging is disabled - integration event {EventName} {MessageId} was not published",
            message.Name, message.Id);
        return Task.CompletedTask;
    }
}
