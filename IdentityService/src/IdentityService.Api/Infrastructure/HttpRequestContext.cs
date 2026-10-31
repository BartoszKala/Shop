using System.Security.Claims;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Domain;

namespace IdentityService.Api.Infrastructure;

internal sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    private const int MaxUserAgentLength = 512;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var userAgent = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(userAgent) ? null
                : userAgent.Length > MaxUserAgentLength ? userAgent[..MaxUserAgentLength]
                : userAgent;
        }
    }

    public UserId? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtClaimNames.Subject), out var id) ? Domain.UserId.From(id) : null;

    public SessionId? SessionId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtClaimNames.SessionId), out var id) ? Domain.SessionId.From(id) : null;
}
