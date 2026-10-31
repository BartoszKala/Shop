using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Contracts;
using IdentityService.Application.Options;
using IdentityService.Contracts;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class PrivacyService(
    UserManager<ApplicationUser> userManager,
    UserProfileBuilder profileBuilder,
    IConsentRepository consents,
    IRefreshTokenRepository refreshTokens,
    ISecurityEventLog securityLog,
    IIntegrationEventPublisher integrationEvents,
    AccountEmailSender emails,
    IRequestContext request,
    IUnitOfWork unitOfWork,
    IOptions<LegalOptions> legalOptions,
    TimeProvider timeProvider)
{
    public async Task<Result<ConsentsResponse>> GetConsentsAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? AccountErrors.NotFound : await profileBuilder.BuildConsentsAsync(user, ct);
    }

    public Task<Result<ConsentsResponse>> UpdateMarketingConsentAsync(
        UserId userId, UpdateMarketingConsentRequest update, CancellationToken ct = default) =>
        unitOfWork.ExecuteInTransactionAsync(async Task<Result<ConsentsResponse>> () =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return AccountErrors.NotFound;
            }

            var current = await profileBuilder.BuildConsentsAsync(user, ct);
            if (current.MarketingConsent == update.Granted)
            {
                return current;
            }

            var now = timeProvider.GetUtcNow();
            var recorded = await RecordAsync(user.Id, ConsentType.Marketing, update.Granted, null, now, ct);
            if (!recorded.IsSuccess)
            {
                return recorded.Error;
            }

            await securityLog.RecordAsync(SecurityEventType.ConsentChanged, user.Id, $"marketing={update.Granted}", ct);
            await integrationEvents.PublishAsync(
                new UserMarketingConsentChanged(user.Id.Value, user.Email!, update.Granted, now), ct);

            return await profileBuilder.BuildConsentsAsync(user, ct);
        }, ct);

    public Task<Result<ConsentsResponse>> AcceptCurrentTermsAsync(UserId userId, CancellationToken ct = default) =>
        unitOfWork.ExecuteInTransactionAsync(async Task<Result<ConsentsResponse>> () =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return AccountErrors.NotFound;
            }

            var legal = legalOptions.Value;
            var now = timeProvider.GetUtcNow();
            foreach (var (type, version) in new[]
                     {
                         (ConsentType.TermsOfService, legal.TermsOfServiceVersion),
                         (ConsentType.PrivacyPolicy, legal.PrivacyPolicyVersion)
                     })
            {
                var recorded = await RecordAsync(user.Id, type, true, version, now, ct);
                if (!recorded.IsSuccess)
                {
                    return recorded.Error;
                }
            }

            await securityLog.RecordAsync(SecurityEventType.ConsentChanged, user.Id,
                $"terms={legal.TermsOfServiceVersion}, privacy={legal.PrivacyPolicyVersion}", ct);

            return await profileBuilder.BuildConsentsAsync(user, ct);
        }, ct);

    public async Task<Result<AccountDataExport>> ExportAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        var events = new List<SecurityEventResponse>();
        for (var page = 1; ; page++)
        {
            var batch = await securityLog.GetForUserAsync(user.Id, new PageRequest { Page = page, PageSize = PageRequest.MaxPageSize }, ct);
            events.AddRange(batch.Items);
            if (page >= batch.TotalPages)
            {
                break;
            }
        }

        var export = new AccountDataExport(
            now,
            await profileBuilder.BuildAsync(user, ct),
            (await consents.GetHistoryAsync(user.Id, ct)).Select(UserProfileBuilder.ToResponse).ToList(),
            await refreshTokens.GetActiveSessionsAsync(user.Id, now, ct),
            events);

        await securityLog.RecordAsync(SecurityEventType.DataExported, user.Id, ct: ct);
        return export;
    }

    public Task<Result<MessageResponse>> DeleteAccountAsync(UserId userId, DeleteAccountRequest deletion, CancellationToken ct = default) =>
        unitOfWork.ExecuteInTransactionAsync(async Task<Result<MessageResponse>> () =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return AccountErrors.NotFound;
            }
            if (!await userManager.CheckPasswordAsync(user, deletion.Password))
            {
                return AccountErrors.InvalidPassword;
            }

            var email = user.Email!;
            await integrationEvents.PublishAsync(new UserDeleted(user.Id.Value, timeProvider.GetUtcNow()), ct);
            await securityLog.RecordAsync(SecurityEventType.AccountDeleted, user.Id, ct: ct);

            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return result.ToError();
            }

            await emails.SendAccountDeletedAsync(email, ct);
            return new MessageResponse("Konto zostało usunięte.");
        }, ct);

    private async Task<Result> RecordAsync(
        UserId userId, ConsentType type, bool granted, string? version, DateTimeOffset now, CancellationToken ct)
    {
        var consent = UserConsent.Create(userId, type, granted, version, now, request.IpAddress);
        if (!consent.IsSuccess)
        {
            return consent.Error;
        }

        await consents.AddAsync(consent.Value, ct);
        return Result.Success();
    }
}
