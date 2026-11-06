using System.Security.Claims;
using ShopService.Application.Abstractions;
using ShopService.Application.Common;
using ShopService.Domain;

namespace ShopService.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context for the current user.");

    public UserId UserId => Domain.UserId.From(Guid.Parse(User.FindFirstValue(JwtClaimNames.Subject)
                                                          ?? throw new InvalidOperationException("Authenticated user has no 'sub' claim.")));

    public string Email => User.FindFirstValue(JwtClaimNames.Email)
                           ?? throw new InvalidOperationException("Authenticated user has no 'email' claim.");
}
