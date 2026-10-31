using System.Security.Cryptography;
using IdentityService.Application.Abstractions;
using IdentityService.Domain;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityService.Infrastructure.Security;

internal sealed class TwoFactorLoginTokenProtector(IDataProtectionProvider dataProtectionProvider) : ITwoFactorLoginTokenProtector
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector("IdentityService.TwoFactorLogin.v1")
        .ToTimeLimitedDataProtector();

    public string Protect(ApplicationUser user) =>
        _protector.Protect($"{user.Id.Value:N}|{user.SecurityStamp}", Lifetime);

    public bool TryUnprotect(string token, out UserId userId, out string securityStamp)
    {
        userId = default;
        securityStamp = string.Empty;
        try
        {
            var parts = _protector.Unprotect(token).Split('|', 2);
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var id))
            {
                return false;
            }

            userId = UserId.From(id);
            securityStamp = parts[1];
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
