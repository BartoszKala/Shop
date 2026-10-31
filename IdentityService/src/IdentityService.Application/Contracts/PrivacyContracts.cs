using System.ComponentModel.DataAnnotations;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Contracts;

public sealed record ConsentEntryResponse(ConsentType Type, bool Granted, string? DocumentVersion, DateTimeOffset RecordedAt);

public sealed record ConsentsResponse(
    bool MarketingConsent,
    string? AcceptedTermsVersion,
    string? AcceptedPrivacyPolicyVersion,
    string CurrentTermsVersion,
    string CurrentPrivacyPolicyVersion,
    IReadOnlyList<ConsentEntryResponse> History);

public sealed record UpdateMarketingConsentRequest
{
    public bool Granted { get; init; }
}

public sealed record DeleteAccountRequest
{
    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string Password { get; init; } = string.Empty;
}

public sealed record AccountDataExport(
    DateTimeOffset ExportedAt,
    UserProfileResponse Profile,
    IReadOnlyList<ConsentEntryResponse> Consents,
    IReadOnlyList<SessionResponse> ActiveSessions,
    IReadOnlyList<SecurityEventResponse> SecurityEvents);
