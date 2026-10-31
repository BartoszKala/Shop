namespace IdentityService.Contracts;

public interface IIntegrationEvent
{
    static abstract string EventName { get; }

    DateTimeOffset OccurredAt { get; }
}

public static class IdentityEvents
{
    public const string Exchange = "identity.events";
}

public sealed record UserRegistered(Guid UserId, string Email, string? FirstName, string? LastName, DateTimeOffset OccurredAt)
    : IIntegrationEvent
{
    public static string EventName => "user.registered";
}

public sealed record UserEmailChanged(Guid UserId, string NewEmail, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventName => "user.email_changed";
}

public sealed record UserDeleted(Guid UserId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventName => "user.deleted";
}

public sealed record UserMarketingConsentChanged(Guid UserId, string Email, bool Granted, DateTimeOffset OccurredAt)
    : IIntegrationEvent
{
    public static string EventName => "user.marketing_consent_changed";
}
