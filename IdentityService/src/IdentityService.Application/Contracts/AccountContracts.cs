using System.ComponentModel.DataAnnotations;
using IdentityService.Domain;

namespace IdentityService.Application.Contracts;

public sealed record UserProfileResponse(
    UserId Id,
    string Email,
    bool EmailConfirmed,
    string? FirstName,
    string? LastName,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    bool MarketingConsent,
    bool RequiresTermsAcceptance);

public sealed record UpdateProfileRequest
{
    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }
}

public sealed record SessionResponse(
    SessionId Id,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActiveAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);

public sealed record ChangePasswordRequest
{
    [Required(ErrorMessage = "Obecne hasło jest wymagane.")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Nowe hasło jest wymagane.")]
    [MinLength(8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków.")]
    [MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record ChangeEmailRequest
{
    [Required(ErrorMessage = "Nowy adres e-mail jest wymagany.")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy adres e-mail.")]
    [MaxLength(256)]
    public string NewEmail { get; init; } = string.Empty;

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string Password { get; init; } = string.Empty;
}

public sealed record ConfirmEmailChangeRequest
{
    [Required]
    public UserId UserId { get; init; }

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Code { get; init; } = string.Empty;
}

public sealed record PasswordRequest
{
    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string Password { get; init; } = string.Empty;
}

public sealed record EnableTwoFactorRequest
{
    [Required(ErrorMessage = "Kod jest wymagany.")]
    [RegularExpression(@"^[0-9\s-]{6,9}$", ErrorMessage = "Kod musi składać się z 6 cyfr.")]
    public string Code { get; init; } = string.Empty;
}

public sealed record TwoFactorStatusResponse(bool IsEnabled, int RecoveryCodesLeft);

public sealed record AuthenticatorSetupResponse(string SharedKey, string AuthenticatorUri, string QrCodeImage);

public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
