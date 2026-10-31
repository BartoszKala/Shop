using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Contracts;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Application.Services;

public sealed class AdminUserService(
    UserManager<ApplicationUser> userManager,
    IAdminUserQueries queries,
    IUserRoleRepository userRoles,
    IRefreshTokenRepository refreshTokens,
    TokenService tokenService,
    AccountEmailSender emails,
    ISecurityEventLog securityLog,
    IRequestContext request,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<PagedResponse<AdminUserListItem>>> SearchAsync(SearchUsersRequest search, CancellationToken ct = default) =>
        await queries.SearchAsync(search.Search, search.ToPageRequest(), ct);

    public async Task<Result<AdminUserDetailsResponse>> GetAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }

        var now = timeProvider.GetUtcNow();
        return new AdminUserDetailsResponse(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            await userManager.HasPasswordAsync(user),
            user.LockoutEnd > now ? user.LockoutEnd : null,
            user.AccessFailedCount,
            await userRoles.GetRolesAsync(user.Id, ct),
            user.CreatedAt,
            user.LastLoginAt,
            await refreshTokens.GetActiveSessionsAsync(user.Id, now, ct));
    }

    public async Task<Result<AdminUserDetailsResponse>> SetRolesAsync(UserId userId, SetRolesRequest change, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }
        if (IsSelf(userId) && !change.Roles.Contains(AppRoles.Admin))
        {
            return AdminErrors.CannotModifySelf;
        }

        var result = await userRoles.SetRolesAsync(user.Id, change.Roles.Distinct().ToList(), ct);
        if (!result.IsSuccess)
        {
            return result.Error;
        }

        await securityLog.RecordAsync(SecurityEventType.RolesChanged, user.Id, string.Join(", ", change.Roles), ct);
        return await GetAsync(userId, ct);
    }

    public async Task<Result<AdminUserDetailsResponse>> LockAsync(UserId userId, LockUserRequest lockRequest, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }
        if (IsSelf(userId))
        {
            return AdminErrors.CannotModifySelf;
        }

        var until = lockRequest.Until?.ToUniversalTime() ?? DateTimeOffset.MaxValue;
        if (until <= timeProvider.GetUtcNow())
        {
            return Error.Validation(nameof(LockUserRequest.Until), "Data blokady musi być w przyszłości.");
        }

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, until);
        await tokenService.RevokeAllAsync(user.Id, ct);
        await securityLog.RecordAsync(SecurityEventType.LockedByAdmin, user.Id,
            $"until {(lockRequest.Until?.ToString("O") ?? "indefinitely")}; {lockRequest.Reason}", ct);

        return await GetAsync(userId, ct);
    }

    public async Task<Result<AdminUserDetailsResponse>> UnlockAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await securityLog.RecordAsync(SecurityEventType.UnlockedByAdmin, user.Id, ct: ct);

        return await GetAsync(userId, ct);
    }

    public Task<Result<MessageResponse>> ForcePasswordResetAsync(UserId userId, CancellationToken ct = default) =>
        unitOfWork.ExecuteInTransactionAsync(async Task<Result<MessageResponse>> () =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return AdminErrors.UserNotFound;
            }

            if (await userManager.HasPasswordAsync(user))
            {
                var removed = await userManager.RemovePasswordAsync(user);
                if (!removed.Succeeded)
                {
                    return removed.ToError();
                }
            }

            await tokenService.RevokeAllAsync(user.Id, ct);
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emails.SendPasswordResetAsync(user, token, forcedByAdmin: true, ct);
            await securityLog.RecordAsync(SecurityEventType.PasswordResetForcedByAdmin, user.Id, ct: ct);

            return new MessageResponse("Hasło zostało usunięte, a użytkownik dostał link do ustawienia nowego.");
        }, ct);

    public async Task<Result> RevokeSessionsAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AdminErrors.UserNotFound;
        }

        await tokenService.RevokeAllAsync(user.Id, ct);
        await securityLog.RecordAsync(SecurityEventType.SessionRevoked, user.Id, "all sessions, by administrator", ct);
        return Result.Success();
    }

    public async Task<Result<PagedResponse<SecurityEventResponse>>> GetSecurityEventsAsync(
        UserId userId, PageRequest page, CancellationToken ct = default) =>
        await securityLog.GetForUserAsync(userId, page, ct);

    private bool IsSelf(UserId userId) => request.UserId == userId;
}
