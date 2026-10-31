using IdentityService.Domain.Common;

namespace IdentityService.Domain.Entities;

public enum ConsentType
{
    TermsOfService,
    PrivacyPolicy,
    Marketing
}

public class UserConsent
{
    public const int VersionMaxLength = 32;

    private UserConsent()
    {
    }

    public UserConsentId Id { get; private set; }
    public UserId UserId { get; private set; }
    public ConsentType Type { get; private set; }

    public string? DocumentVersion { get; private set; }

    public bool Granted { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string? IpAddress { get; private set; }

    public static Result<UserConsent> Create(
        UserId userId, ConsentType type, bool granted, string? documentVersion, DateTimeOffset now, string? ipAddress)
    {
        if (type is ConsentType.TermsOfService or ConsentType.PrivacyPolicy)
        {
            if (string.IsNullOrWhiteSpace(documentVersion))
            {
                return Error.Validation(nameof(DocumentVersion), "Wersja dokumentu jest wymagana.");
            }
            if (!granted)
            {
                return Error.Validation(nameof(Granted), "Akceptacja regulaminu i polityki prywatności jest wymagana.");
            }
        }
        if (documentVersion?.Length > VersionMaxLength)
        {
            return Error.Validation(nameof(DocumentVersion), "Wersja dokumentu jest za długa.");
        }

        return new UserConsent
        {
            Id = UserConsentId.New(),
            UserId = userId,
            Type = type,
            Granted = granted,
            DocumentVersion = documentVersion,
            RecordedAt = now,
            IpAddress = ipAddress
        };
    }
}
