using System.ComponentModel.DataAnnotations;

namespace IdentityService.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, Url]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; set; } = 14;
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    public string ConfirmEmailPath { get; set; } = "/account/confirm-email";
    public string ConfirmEmailChangePath { get; set; } = "/account/confirm-email-change";
    public string ResetPasswordPath { get; set; } = "/account/reset-password";
}

public sealed class LegalOptions
{
    public const string SectionName = "Legal";

    [Required, MaxLength(32)]
    public string TermsOfServiceVersion { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string PrivacyPolicyVersion { get; set; } = string.Empty;
}

public sealed class TwoFactorOptions
{
    public const string SectionName = "TwoFactor";

    [Required]
    public string AuthenticatorIssuer { get; set; } = "Sklep";
}
