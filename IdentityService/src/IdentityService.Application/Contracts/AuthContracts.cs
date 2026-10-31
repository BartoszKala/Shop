using System.ComponentModel.DataAnnotations;
using IdentityService.Domain;

namespace IdentityService.Application.Contracts;

public sealed record RegisterRequest
{
    [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy adres e-mail.")]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    [MinLength(8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków.")]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Akceptacja regulaminu i polityki prywatności jest wymagana.")]
    public bool AcceptTerms { get; init; }

    public bool MarketingConsent { get; init; }
}

public sealed record ConfirmEmailRequest
{
    [Required]
    public UserId UserId { get; init; }

    [Required]
    public string Code { get; init; } = string.Empty;
}

public sealed record EmailRequest
{
    [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy adres e-mail.")]
    public string Email { get; init; } = string.Empty;
}

public sealed record LoginRequest
{
    [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy adres e-mail.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string Password { get; init; } = string.Empty;
}

public sealed record TwoFactorLoginRequest
{
    [Required]
    public string TwoFactorToken { get; init; } = string.Empty;

    [Required(ErrorMessage = "Kod jest wymagany.")]
    [RegularExpression(@"^[0-9\s-]{6,9}$", ErrorMessage = "Kod musi składać się z 6 cyfr.")]
    public string Code { get; init; } = string.Empty;
}

public sealed record RecoveryCodeLoginRequest
{
    [Required]
    public string TwoFactorToken { get; init; } = string.Empty;

    [Required(ErrorMessage = "Kod odzyskiwania jest wymagany.")]
    [MaxLength(32)]
    public string RecoveryCode { get; init; } = string.Empty;
}

public sealed record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Code { get; init; } = string.Empty;

    [Required(ErrorMessage = "Nowe hasło jest wymagane.")]
    [MinLength(8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków.")]
    [MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record AuthTokensResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
{
    public string TokenType => "Bearer";
}

public sealed record LoginResponse(bool RequiresTwoFactor, string? TwoFactorToken, AuthTokensResponse? Tokens)
{
    public static LoginResponse Success(AuthTokensResponse tokens) => new(false, null, tokens);
    public static LoginResponse TwoFactorRequired(string twoFactorToken) => new(true, twoFactorToken, null);
}

public sealed record MessageResponse(string Message);
