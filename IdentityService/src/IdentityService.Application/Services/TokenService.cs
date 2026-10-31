using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Contracts;
using IdentityService.Application.Options;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class TokenService(
    IAccessTokenGenerator accessTokenGenerator,
    IRefreshTokenRepository refreshTokens,
    UserManager<ApplicationUser> userManager,
    IUserRoleRepository userRoles,
    ISecurityEventLog securityLog,
    IRequestContext request,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider)
{
    public Task<Result<AuthTokensResponse>> IssueAsync(ApplicationUser user, CancellationToken ct = default) =>
        IssueForSessionAsync(user, SessionId.New(), ct);

    public async Task<Result<AuthTokensResponse>> RefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var existing = await refreshTokens.FindByHashAsync(Hash(rawRefreshToken), ct);
        if (existing is null)
        {
            return AuthErrors.SessionExpired;
        }

        if (existing.WasRotated)
        {
            await RevokeAllAsync(existing.UserId, ct);
            await securityLog.RecordAsync(SecurityEventType.RefreshTokenReuseDetected, existing.UserId, ct: ct);
            return AuthErrors.SessionExpired;
        }

        var user = existing.User;
        if (!user.EmailConfirmed || await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.SessionExpired;
        }

        var newRawToken = NewRawToken();
        var newToken = CreateRefreshToken(user.Id, existing.SessionId, newRawToken);
        if (!newToken.IsSuccess)
        {
            return newToken.Error;
        }

        var rotated = existing.Rotate(timeProvider.GetUtcNow(), newToken.Value.TokenHash);
        if (!rotated.IsSuccess)
        {
            return AuthErrors.SessionExpired;
        }

        refreshTokens.Add(newToken.Value);
        await refreshTokens.SaveChangesAsync(ct);

        return await CreateResponseAsync(user, newRawToken, newToken.Value);
    }

    public async Task<UserId?> EndSessionAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var token = await refreshTokens.FindByHashAsync(Hash(rawRefreshToken), ct);
        if (token is null)
        {
            return null;
        }

        await refreshTokens.RevokeSessionAsync(token.UserId, token.SessionId, timeProvider.GetUtcNow(), ct);
        return token.UserId;
    }

    public Task RevokeAllAsync(UserId userId, CancellationToken ct = default) =>
        refreshTokens.RevokeAllForUserAsync(userId, timeProvider.GetUtcNow(), ct);

    private async Task<Result<AuthTokensResponse>> IssueForSessionAsync(ApplicationUser user, SessionId sessionId, CancellationToken ct)
    {
        var rawRefreshToken = NewRawToken();
        var refreshToken = CreateRefreshToken(user.Id, sessionId, rawRefreshToken);
        if (!refreshToken.IsSuccess)
        {
            return refreshToken.Error;
        }

        refreshTokens.Add(refreshToken.Value);
        await refreshTokens.SaveChangesAsync(ct);

        return await CreateResponseAsync(user, rawRefreshToken, refreshToken.Value);
    }

    private async Task<AuthTokensResponse> CreateResponseAsync(ApplicationUser user, string rawRefreshToken, RefreshToken refreshToken)
    {
        var roles = await userRoles.GetRolesAsync(user.Id);
        var accessToken = accessTokenGenerator.Generate(user, roles, refreshToken.SessionId);
        return new AuthTokensResponse(accessToken.Token, accessToken.ExpiresAt, rawRefreshToken, refreshToken.ExpiresAt);
    }

    private Result<RefreshToken> CreateRefreshToken(UserId userId, SessionId sessionId, string rawToken) =>
        RefreshToken.Create(
            userId,
            sessionId,
            Hash(rawToken),
            timeProvider.GetUtcNow(),
            TimeSpan.FromDays(jwtOptions.Value.RefreshTokenLifetimeDays),
            request.IpAddress,
            request.UserAgent);

    private static string NewRawToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
