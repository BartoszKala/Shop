using IdentityService.Application.Abstractions;
using IdentityService.Application.Contracts;
using IdentityService.Application.Options;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class UserProfileBuilder(
    IUserRoleRepository userRoles,
    IConsentRepository consents,
    IOptions<LegalOptions> legalOptions)
{
    public async Task<UserProfileResponse> BuildAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var roles = await userRoles.GetRolesAsync(user.Id, ct);
        var state = ConsentState.From(await consents.GetHistoryAsync(user.Id, ct));
        var legal = legalOptions.Value;

        return new UserProfileResponse(
            user.Id,
            user.Email!,
            user.EmailConfirmed,
            user.FirstName,
            user.LastName,
            user.TwoFactorEnabled,
            roles,
            user.CreatedAt,
            state.Marketing,
            state.TermsVersion != legal.TermsOfServiceVersion || state.PrivacyVersion != legal.PrivacyPolicyVersion);
    }

    public async Task<ConsentsResponse> BuildConsentsAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var history = await consents.GetHistoryAsync(user.Id, ct);
        var state = ConsentState.From(history);
        var legal = legalOptions.Value;

        return new ConsentsResponse(
            state.Marketing,
            state.TermsVersion,
            state.PrivacyVersion,
            legal.TermsOfServiceVersion,
            legal.PrivacyPolicyVersion,
            history.Select(ToResponse).ToList());
    }

    public static ConsentEntryResponse ToResponse(UserConsent consent) =>
        new(consent.Type, consent.Granted, consent.DocumentVersion, consent.RecordedAt);

    private sealed record ConsentState(bool Marketing, string? TermsVersion, string? PrivacyVersion)
    {
        public static ConsentState From(IReadOnlyList<UserConsent> history) => new(
            history.FirstOrDefault(c => c.Type == ConsentType.Marketing)?.Granted ?? false,
            history.FirstOrDefault(c => c.Type == ConsentType.TermsOfService && c.Granted)?.DocumentVersion,
            history.FirstOrDefault(c => c.Type == ConsentType.PrivacyPolicy && c.Granted)?.DocumentVersion);
    }
}
