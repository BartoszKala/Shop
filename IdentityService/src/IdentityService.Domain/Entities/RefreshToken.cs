using IdentityService.Domain.Common;

namespace IdentityService.Domain.Entities;

public class RefreshToken
{
    public const int UserAgentMaxLength = 512;

    private RefreshToken()
    {
    }

    public RefreshTokenId Id { get; private set; }

    public UserId UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    public SessionId SessionId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? UserAgent { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool WasRotated => ReplacedByTokenHash is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public static Result<RefreshToken> Create(
        UserId userId,
        SessionId sessionId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? createdByIp,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return Error.Validation(nameof(TokenHash), "Token hash is required.");
        }
        if (lifetime <= TimeSpan.Zero)
        {
            return Error.Validation(nameof(ExpiresAt), "Token lifetime must be positive.");
        }

        return new RefreshToken
        {
            Id = RefreshTokenId.New(),
            UserId = userId,
            SessionId = sessionId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
            CreatedByIp = createdByIp,
            UserAgent = userAgent is { Length: > UserAgentMaxLength } ? userAgent[..UserAgentMaxLength] : userAgent
        };
    }

    public Result Rotate(DateTimeOffset now, string replacedByTokenHash)
    {
        if (IsRevoked)
        {
            return Error.Conflict("token.revoked", "Token został już unieważniony.");
        }
        if (IsExpired(now))
        {
            return Error.Unauthorized("token.expired", "Token wygasł.");
        }

        RevokedAt = now;
        ReplacedByTokenHash = replacedByTokenHash;
        return Result.Success();
    }
}
