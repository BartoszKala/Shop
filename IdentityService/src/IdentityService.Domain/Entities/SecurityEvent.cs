namespace IdentityService.Domain.Entities;

public enum SecurityEventType
{
    Registered,
    EmailConfirmed,
    LoginSucceeded,
    LoginFailed,
    LockedOut,
    TwoFactorFailed,
    RecoveryCodeUsed,
    RefreshTokenReuseDetected,
    LoggedOut,
    SessionRevoked,
    PasswordChanged,
    PasswordResetRequested,
    PasswordReset,
    EmailChangeRequested,
    EmailChanged,
    TwoFactorEnabled,
    TwoFactorDisabled,
    RecoveryCodesRegenerated,
    RolesChanged,
    LockedByAdmin,
    UnlockedByAdmin,
    PasswordResetForcedByAdmin,
    ConsentChanged,
    DataExported,
    AccountDeleted
}

public class SecurityEvent
{
    public const int UserAgentMaxLength = 512;
    public const int DetailsMaxLength = 1000;

    private SecurityEvent()
    {
    }

    public SecurityEventId Id { get; private set; }

    public UserId? UserId { get; private set; }

    public UserId? ActorId { get; private set; }

    public SecurityEventType Type { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Details { get; private set; }

    public static SecurityEvent Create(
        SecurityEventType type,
        UserId? userId,
        UserId? actorId,
        DateTimeOffset now,
        string? ipAddress,
        string? userAgent,
        string? details) => new()
    {
        Id = SecurityEventId.New(),
        Type = type,
        UserId = userId,
        ActorId = actorId,
        OccurredAt = now,
        IpAddress = ipAddress,
        UserAgent = Truncate(userAgent, UserAgentMaxLength),
        Details = Truncate(details, DetailsMaxLength)
    };

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
