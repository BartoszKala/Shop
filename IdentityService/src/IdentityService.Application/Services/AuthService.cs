using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Contracts;
using IdentityService.Application.Options;
using IdentityService.Contracts;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IUserRoleRepository userRoles,
    IConsentRepository consents,
    IRefreshTokenRepository refreshTokens,
    TokenService tokenService,
    ITwoFactorLoginTokenProtector twoFactorProtector,
    AccountEmailSender emails,
    ISecurityEventLog securityLog,
    IIntegrationEventPublisher integrationEvents,
    IRequestContext request,
    IUnitOfWork unitOfWork,
    IOptions<LegalOptions> legalOptions,
    TimeProvider timeProvider)
{
    public Task<Result<MessageResponse>> RegisterAsync(RegisterRequest registration, CancellationToken ct = default) =>
        unitOfWork.ExecuteInTransactionAsync(async Task<Result<MessageResponse>> () =>
        {
            var now = timeProvider.GetUtcNow();
            var created = ApplicationUser.Create(registration.Email, registration.FirstName, registration.LastName, now);
            if (!created.IsSuccess)
            {
                return created.Error;
            }

            var user = created.Value;
            var result = await userManager.CreateAsync(user, registration.Password);
            if (!result.Succeeded)
            {
                return result.ToError();
            }

            var roleAdded = await userRoles.AddToRoleAsync(user.Id, AppRoles.User, ct);
            if (!roleAdded.IsSuccess)
            {
                return roleAdded.Error;
            }

            var consentsRecorded = await RecordRegistrationConsentsAsync(user.Id, registration.MarketingConsent, now, ct);
            if (!consentsRecorded.IsSuccess)
            {
                return consentsRecorded.Error;
            }

            await securityLog.RecordAsync(SecurityEventType.Registered, user.Id, ct: ct);
            await integrationEvents.PublishAsync(
                new UserRegistered(user.Id.Value, user.Email!, user.FirstName, user.LastName, now), ct);

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emails.SendEmailConfirmationAsync(user, token, ct);

            return new MessageResponse("Konto zostało utworzone. Sprawdź skrzynkę e-mail, aby je aktywować.");
        }, ct);

    public async Task<Result<MessageResponse>> ConfirmEmailAsync(ConfirmEmailRequest confirmation, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(confirmation.UserId.ToString());
        if (user is null || !TokenEncoding.TryDecode(confirmation.Code, out var token))
        {
            return AuthErrors.InvalidLink;
        }

        if (!user.EmailConfirmed)
        {
            var result = await userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                return AuthErrors.InvalidLink;
            }

            await securityLog.RecordAsync(SecurityEventType.EmailConfirmed, user.Id, ct: ct);
        }

        return new MessageResponse("Adres e-mail został potwierdzony. Możesz się zalogować.");
    }

    public async Task<Result<MessageResponse>> ResendConfirmationAsync(EmailRequest emailRequest, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(emailRequest.Email);
        if (user is { EmailConfirmed: false })
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emails.SendEmailConfirmationAsync(user, token, ct);
        }

        return new MessageResponse("Jeśli konto istnieje i nie zostało aktywowane, wysłaliśmy nowy link.");
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest login, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(login.Email);
        if (user is null)
        {
            await securityLog.RecordAsync(SecurityEventType.LoginFailed, null, "unknown account", ct);
            return AuthErrors.InvalidCredentials;
        }
        if (await userManager.IsLockedOutAsync(user))
        {
            await securityLog.RecordAsync(SecurityEventType.LoginFailed, user.Id, "locked out", ct);
            return AuthErrors.LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, login.Password))
        {
            return await RegisterFailedAttemptAsync(user, SecurityEventType.LoginFailed, "wrong password", AuthErrors.InvalidCredentials, ct);
        }

        if (!user.EmailConfirmed)
        {
            await securityLog.RecordAsync(SecurityEventType.LoginFailed, user.Id, "e-mail not confirmed", ct);
            return AuthErrors.EmailNotConfirmed;
        }

        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            return LoginResponse.TwoFactorRequired(twoFactorProtector.Protect(user));
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CompleteLoginAsync(user, "password", ct);
    }

    public async Task<Result<LoginResponse>> LoginWithTwoFactorAsync(TwoFactorLoginRequest login, CancellationToken ct = default)
    {
        var user = await FindTwoFactorUserAsync(login.TwoFactorToken);
        if (user is null)
        {
            return AuthErrors.TwoFactorSessionExpired;
        }
        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.LockedOut;
        }

        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(login.Code));
        if (!valid)
        {
            return await RegisterFailedAttemptAsync(user, SecurityEventType.TwoFactorFailed, null, AuthErrors.InvalidTwoFactorCode, ct);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CompleteLoginAsync(user, "password+authenticator", ct);
    }

    public async Task<Result<LoginResponse>> LoginWithRecoveryCodeAsync(RecoveryCodeLoginRequest login, CancellationToken ct = default)
    {
        var user = await FindTwoFactorUserAsync(login.TwoFactorToken);
        if (user is null)
        {
            return AuthErrors.TwoFactorSessionExpired;
        }
        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.LockedOut;
        }

        var result = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, login.RecoveryCode.Replace(" ", string.Empty));
        if (!result.Succeeded)
        {
            return await RegisterFailedAttemptAsync(user, SecurityEventType.TwoFactorFailed, "recovery code", AuthErrors.InvalidRecoveryCode, ct);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await securityLog.RecordAsync(SecurityEventType.RecoveryCodeUsed, user.Id,
            $"{await userManager.CountRecoveryCodesAsync(user)} codes left", ct);
        return await CompleteLoginAsync(user, "password+recovery_code", ct);
    }

    public Task<Result<AuthTokensResponse>> RefreshAsync(RefreshTokenRequest refresh, CancellationToken ct = default) =>
        tokenService.RefreshAsync(refresh.RefreshToken, ct);

    public async Task<Result> LogoutAsync(RefreshTokenRequest logout, CancellationToken ct = default)
    {
        var userId = await tokenService.EndSessionAsync(logout.RefreshToken, ct);
        if (userId is not null)
        {
            await securityLog.RecordAsync(SecurityEventType.LoggedOut, userId, ct: ct);
        }

        return Result.Success();
    }

    public async Task<Result<MessageResponse>> ForgotPasswordAsync(EmailRequest emailRequest, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(emailRequest.Email);
        if (user is { EmailConfirmed: true })
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emails.SendPasswordResetAsync(user, token, forcedByAdmin: false, ct);
            await securityLog.RecordAsync(SecurityEventType.PasswordResetRequested, user.Id, ct: ct);
        }

        return new MessageResponse("Jeśli konto istnieje, wysłaliśmy link do zresetowania hasła.");
    }

    public async Task<Result<MessageResponse>> ResetPasswordAsync(ResetPasswordRequest reset, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(reset.Email);
        if (user is null || !TokenEncoding.TryDecode(reset.Code, out var token))
        {
            return AuthErrors.InvalidLink;
        }

        var result = await userManager.ResetPasswordAsync(user, token, reset.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? AuthErrors.InvalidLink
                : result.ToError();
        }

        await tokenService.RevokeAllAsync(user.Id, ct);
        await securityLog.RecordAsync(SecurityEventType.PasswordReset, user.Id, ct: ct);
        await emails.SendPasswordChangedNotificationAsync(user, ct);

        return new MessageResponse("Hasło zostało zmienione. Możesz się zalogować.");
    }

    internal static string NormalizeCode(string code) =>
        code.Replace(" ", string.Empty).Replace("-", string.Empty);

    private async Task<Result<LoginResponse>> CompleteLoginAsync(ApplicationUser user, string method, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var isNewDevice = await refreshTokens.IsNewDeviceAsync(user.Id, request.UserAgent, ct);

        user.RecordLogin(now);
        await userManager.UpdateAsync(user);

        var tokens = await tokenService.IssueAsync(user, ct);
        if (!tokens.IsSuccess)
        {
            return tokens.Error;
        }

        await securityLog.RecordAsync(SecurityEventType.LoginSucceeded, user.Id, method, ct);
        if (isNewDevice)
        {
            await emails.SendNewDeviceLoginNotificationAsync(user, now, request.IpAddress, request.UserAgent, ct);
        }

        return LoginResponse.Success(tokens.Value);
    }

    private async Task<Error> RegisterFailedAttemptAsync(
        ApplicationUser user, SecurityEventType type, string? details, Error error, CancellationToken ct)
    {
        await userManager.AccessFailedAsync(user);
        if (await userManager.IsLockedOutAsync(user))
        {
            await securityLog.RecordAsync(SecurityEventType.LockedOut, user.Id, ct: ct);
            return AuthErrors.LockedOut;
        }

        await securityLog.RecordAsync(type, user.Id, details, ct);
        return error;
    }

    private async Task<Result> RecordRegistrationConsentsAsync(UserId userId, bool marketing, DateTimeOffset now, CancellationToken ct)
    {
        var legal = legalOptions.Value;
        var entries = new List<Result<UserConsent>>
        {
            UserConsent.Create(userId, ConsentType.TermsOfService, true, legal.TermsOfServiceVersion, now, request.IpAddress),
            UserConsent.Create(userId, ConsentType.PrivacyPolicy, true, legal.PrivacyPolicyVersion, now, request.IpAddress)
        };
        if (marketing)
        {
            entries.Add(UserConsent.Create(userId, ConsentType.Marketing, true, null, now, request.IpAddress));
        }

        foreach (var entry in entries)
        {
            if (!entry.IsSuccess)
            {
                return entry.Error;
            }

            await consents.AddAsync(entry.Value, ct);
        }

        return Result.Success();
    }

    private async Task<ApplicationUser?> FindTwoFactorUserAsync(string twoFactorToken)
    {
        if (!twoFactorProtector.TryUnprotect(twoFactorToken, out var userId, out var securityStamp))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && user.TwoFactorEnabled && user.SecurityStamp == securityStamp ? user : null;
    }
}
