using IdentityService.Application.Common;
using IdentityService.Application.Options;
using IdentityService.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route(".well-known")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class WellKnownController(IOptions<JwtOptions> jwtOptions, SigningKeyProvider signingKeys) : ControllerBase
{
    [HttpGet("openid-configuration")]
    public IActionResult Configuration()
    {
        var issuer = jwtOptions.Value.Issuer.TrimEnd('/');
        return Ok(new
        {
            issuer = jwtOptions.Value.Issuer,
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            token_endpoint = $"{issuer}/api/auth/login",
            id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.RsaSha256 },
            claims_supported = new[]
            {
                JwtClaimNames.Subject, JwtClaimNames.Email, JwtClaimNames.EmailVerified, JwtClaimNames.Name,
                JwtClaimNames.GivenName, JwtClaimNames.FamilyName, JwtClaimNames.Role
            }
        });
    }

    [HttpGet("jwks.json")]
    public IActionResult Jwks() => Ok(signingKeys.Jwks);
}
