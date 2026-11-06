using System.ComponentModel.DataAnnotations;

namespace ShopService.Api;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Adres IdentityService (= <c>iss</c> w tokenie); stąd pobierany jest dokument OpenID i klucze JWKS.</summary>
    [Required, Url]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;
}
