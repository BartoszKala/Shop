using System.Security.Claims;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Options;
using IdentityService.Domain;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Security;

internal sealed class JwtAccessTokenGenerator(
    IOptions<JwtOptions> jwtOptions,
    SigningKeyProvider signingKeys,
    TimeProvider timeProvider) : IAccessTokenGenerator
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public AccessToken Generate(ApplicationUser user, IEnumerable<string> roles, SessionId sessionId)
    {
        var options = jwtOptions.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtClaimNames.Subject, user.Id.ToString()),
            new(JwtClaimNames.JwtId, Guid.NewGuid().ToString()),
            new(JwtClaimNames.SessionId, sessionId.ToString()),
            new(JwtClaimNames.Email, user.Email!),
            new(JwtClaimNames.EmailVerified, user.EmailConfirmed ? "true" : "false", ClaimValueTypes.Boolean),
            new(JwtClaimNames.Name, user.FullName ?? user.Email!)
        };
        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            claims.Add(new Claim(JwtClaimNames.GivenName, user.FirstName));
        }
        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            claims.Add(new Claim(JwtClaimNames.FamilyName, user.LastName));
        }
        claims.AddRange(roles.Select(role => new Claim(JwtClaimNames.Role, role)));

        var token = TokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = signingKeys.SigningCredentials
        });

        return new AccessToken(token, expiresAt);
    }
}
