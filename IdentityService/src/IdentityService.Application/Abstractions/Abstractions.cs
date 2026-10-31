using IdentityService.Application.Contracts;
using IdentityService.Contracts;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Abstractions;

public interface IRequestContext
{
    string? IpAddress { get; }
    string? UserAgent { get; }

    UserId? UserId { get; }

    SessionId? SessionId { get; }
}

public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct = default)
        where TResult : Result;
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IAccessTokenGenerator
{
    AccessToken Generate(ApplicationUser user, IEnumerable<string> roles, SessionId sessionId);
}

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);

    Task RevokeAllForUserAsync(UserId userId, DateTimeOffset now, CancellationToken ct = default);

    Task<int> RevokeSessionAsync(UserId userId, SessionId sessionId, DateTimeOffset now, CancellationToken ct = default);

    Task RevokeAllExceptSessionAsync(UserId userId, SessionId keepSessionId, DateTimeOffset now, CancellationToken ct = default);

    Task<IReadOnlyList<SessionResponse>> GetActiveSessionsAsync(UserId userId, DateTimeOffset now, CancellationToken ct = default);

    Task<bool> IsNewDeviceAsync(UserId userId, string? userAgent, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRoleRepository
{
    Task<IReadOnlyList<string>> GetRolesAsync(UserId userId, CancellationToken ct = default);

    Task<Result> AddToRoleAsync(UserId userId, string role, CancellationToken ct = default);

    Task<Result> SetRolesAsync(UserId userId, IReadOnlyCollection<string> roles, CancellationToken ct = default);
}

public interface IConsentRepository
{
    Task AddAsync(UserConsent consent, CancellationToken ct = default);

    Task<IReadOnlyList<UserConsent>> GetHistoryAsync(UserId userId, CancellationToken ct = default);
}

public interface ISecurityEventLog
{
    Task RecordAsync(SecurityEventType type, UserId? userId, string? details = null, CancellationToken ct = default);

    Task<PagedResponse<SecurityEventResponse>> GetForUserAsync(UserId userId, PageRequest page, CancellationToken ct = default);
}

public interface IAdminUserQueries
{
    Task<PagedResponse<AdminUserListItem>> SearchAsync(string? search, PageRequest page, CancellationToken ct = default);
}

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default) where TEvent : IIntegrationEvent;
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public interface ITwoFactorLoginTokenProtector
{
    string Protect(ApplicationUser user);

    bool TryUnprotect(string token, out UserId userId, out string securityStamp);
}

public interface IQrCodeGenerator
{
    string GeneratePngDataUrl(string text);
}
