using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdentityService.Infrastructure.Email;
using IdentityService.Infrastructure.Outbox;

namespace IdentityService.IntegrationTests.Infrastructure;

public sealed partial class FakeMailbox : IEmailDelivery
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public Task DeliverAsync(EmailMessage message, CancellationToken ct = default)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string recipient) =>
        _messages.Where(m => m.To.Equals(recipient, StringComparison.OrdinalIgnoreCase)).ToList();

    public async Task<EmailMessage> WaitForAsync(string recipient, string subject, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            var message = To(recipient).LastOrDefault(m => m.Subject == subject);
            if (message is not null)
            {
                return message;
            }
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"No e-mail '{subject}' for {recipient}. Received: {string.Join(", ", To(recipient).Select(m => m.Subject))}");
    }

    public static Dictionary<string, string> LinkParameters(EmailMessage message)
    {
        var href = WebUtility.HtmlDecode(HrefRegex().Match(message.HtmlBody).Groups[1].Value);
        return new Uri(href).Query.TrimStart('?').Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
    }

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex HrefRegex();
}

public sealed class FakeIntegrationEventSender : IIntegrationEventSender
{
    private readonly ConcurrentQueue<OutboxMessage> _messages = new();

    public Task SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public async Task<JsonElement> WaitForAsync(string eventName, Guid userId, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            foreach (var message in _messages.Where(m => m.Name == eventName))
            {
                var payload = JsonDocument.Parse(message.Payload).RootElement;
                if (payload.GetProperty("userId").GetGuid() == userId)
                {
                    return payload;
                }
            }
            await Task.Delay(100);
        }

        throw new TimeoutException($"No integration event '{eventName}' for user {userId}.");
    }
}
