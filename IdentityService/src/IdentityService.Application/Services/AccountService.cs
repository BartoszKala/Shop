using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Contracts;
using IdentityService.Contracts;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Application.Services;

public sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    UserProfileBuilder profileBuilder,
    IRefreshTokenRepository refreshTokens,
    TokenService tokenService,
    AccountEmailSender emails,
    ISecurityEventLog securityLog,
    IIntegrationEventPublisher integrationEvents,
    IRequestContext request,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<UserProfileResponse>> GetProfileAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? AccountErrors.NotFound : await profileBuilder.BuildAsync(user, ct);
    }

    public async Task<Result<UserProfileResponse>> UpdateProfileAsync(UserId userId, UpdateProfileRequest update, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }

        var changed = user.ChangeName(update.FirstName, update.LastName);
        if (!changed.IsSuccess)
        {
            return changed.Error;
        }

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? await profileBuilder.BuildAsync(user, ct) : result.ToError();
    }

    public Task<Result<AuthTokensResponse>> ChangePasswordAsync(UserId userId, ChangePasswordRequest change, CancellationToken ct = default) =>
        unitOfWork.ExecuteInTransactionAsync(async Task<Result<AuthTokensResponse>> () =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return AccountErrors.NotFound;
            }

            var result = await userManager.ChangePasswordAsync(user, change.CurrentPassword, change.NewPassword);
            if (!result.Succeeded)
            {
                return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                    ? AccountErrors.InvalidPassword
                    : result.ToError();
            }

            await tokenService.RevokeAllAsync(user.Id, ct);
            await securityLog.RecordAsync(SecurityEventType.PasswordChanged, user.Id, ct: ct);
            await emails.SendPasswordChangedNotificationAsync(user, ct);

            return await tokenService.IssueAsync(user, ct);
        }, ct);

    public async Task<Result<MessageResponse>> ChangeEmailAsync(UserId userId, ChangeEmailRequest change, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }
        if (!await userManager.CheckPasswordAsync(user, change.Password))
        {
            return AccountErrors.InvalidPassword;
        }
        if (string.Equals(user.Email, change.NewEmail, StringComparison.OrdinalIgnoreCase))
        {
            return AccountErrors.SameEmail;
        }
        if (await userManager.FindByEmailAsync(change.NewEmail) is not null)
        {
            return AccountErrors.EmailTaken;
        }

        var token = await userManager.GenerateChangeEmailTokenAsync(user, change.NewEmail);
        await emails.SendEmailChangeConfirmationAsync(user, change.NewEmail, token, ct);
        await securityLog.RecordAsync(SecurityEventType.EmailChangeRequested, user.Id, ct: ct);

        return new MessageResponse("Wysłaliśmy link potwierdzający na nowy adres e-mail.");
    }

    public Task<Result<MessageResponse>> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest confirmation, CancellationToken ct = default) =>
        unitOfWork.ExecuteInTransactionAsync(async Task<Result<MessageResponse>> () =>
        {
            var user = await userManager.FindByIdAsync(confirmation.UserId.ToString());
            if (user is null || !TokenEncoding.TryDecode(confirmation.Code, out var token))
            {
                return AuthErrors.InvalidLink;
            }

            var oldEmail = user.Email!;
            var result = await userManager.ChangeEmailAsync(user, confirmation.Email, token);
            if (!result.Succeeded)
            {
                return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                    ? AuthErrors.InvalidLink
                    : result.ToError();
            }

            result = await userManager.SetUserNameAsync(user, confirmation.Email);
            if (!result.Succeeded)
            {
                return result.ToError();
            }

            await securityLog.RecordAsync(SecurityEventType.EmailChanged, user.Id, ct: ct);
            await integrationEvents.PublishAsync(
                new UserEmailChanged(user.Id.Value, confirmation.Email, timeProvider.GetUtcNow()), ct);
            await emails.SendEmailChangedNotificationAsync(oldEmail, confirmation.Email, ct);

            return new MessageResponse("Adres e-mail został zmieniony.");
        }, ct);

    public async Task<Result<IReadOnlyList<SessionResponse>>> GetSessionsAsync(UserId userId, CancellationToken ct = default)
    {
        var sessions = await refreshTokens.GetActiveSessionsAsync(userId, timeProvider.GetUtcNow(), ct);
        return sessions.Select(s => s with { IsCurrent = s.Id == request.SessionId }).ToList();
    }

    public async Task<Result> RevokeSessionAsync(UserId userId, SessionId sessionId, CancellationToken ct = default)
    {
        var revoked = await refreshTokens.RevokeSessionAsync(userId, sessionId, timeProvider.GetUtcNow(), ct);
        if (revoked == 0)
        {
            return AccountErrors.SessionNotFound;
        }

        await securityLog.RecordAsync(SecurityEventType.SessionRevoked, userId, $"session {sessionId}", ct);
        return Result.Success();
    }

    public async Task<Result> RevokeOtherSessionsAsync(UserId userId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        if (request.SessionId is { } current)
        {
            await refreshTokens.RevokeAllExceptSessionAsync(userId, current, now, ct);
        }
        else
        {
            await refreshTokens.RevokeAllForUserAsync(userId, now, ct);
        }

        await securityLog.RecordAsync(SecurityEventType.SessionRevoked, userId, "all other sessions", ct);
        return Result.Success();
    }

    public async Task<Result<PagedResponse<SecurityEventResponse>>> GetSecurityEventsAsync(
        UserId userId, PageRequest page, CancellationToken ct = default) =>
        await securityLog.GetForUserAsync(userId, page, ct);
}
