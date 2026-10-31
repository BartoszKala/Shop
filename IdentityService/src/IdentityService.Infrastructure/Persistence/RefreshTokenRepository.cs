using IdentityService.Application.Abstractions;
using IdentityService.Application.Contracts;
using IdentityService.Domain;
using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

internal sealed class RefreshTokenRepository(AppIdentityDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task RevokeAllForUserAsync(UserId userId, DateTimeOffset now, CancellationToken ct = default) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    public Task<int> RevokeSessionAsync(UserId userId, SessionId sessionId, DateTimeOffset now, CancellationToken ct = default) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.SessionId == sessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    public Task RevokeAllExceptSessionAsync(UserId userId, SessionId keepSessionId, DateTimeOffset now, CancellationToken ct = default) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.SessionId != keepSessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    public async Task<IReadOnlyList<SessionResponse>> GetActiveSessionsAsync(UserId userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var active = await db.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        if (active.Count == 0)
        {
            return [];
        }

        var sessionIds = active.Select(t => t.SessionId).Distinct().ToList();
        var starts = await db.RefreshTokens
            .Where(t => t.UserId == userId && sessionIds.Contains(t.SessionId))
            .GroupBy(t => t.SessionId)
            .Select(g => new { SessionId = g.Key, StartedAt = g.Min(t => t.CreatedAt) })
            .ToDictionaryAsync(s => s.SessionId, s => s.StartedAt, ct);

        return active
            .GroupBy(t => t.SessionId)
            .Select(g => g.MaxBy(t => t.CreatedAt)!)
            .Select(t => new SessionResponse(
                t.SessionId, starts.GetValueOrDefault(t.SessionId, t.CreatedAt), t.CreatedAt, t.CreatedByIp, t.UserAgent, false))
            .OrderByDescending(s => s.LastActiveAt)
            .ToList();
    }

    public async Task<bool> IsNewDeviceAsync(UserId userId, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userAgent) || !await db.RefreshTokens.AnyAsync(t => t.UserId == userId, ct))
        {
            return false;
        }

        return !await db.RefreshTokens.AnyAsync(t => t.UserId == userId && t.UserAgent == userAgent, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
