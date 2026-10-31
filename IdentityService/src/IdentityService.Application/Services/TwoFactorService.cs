using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Contracts;
using IdentityService.Application.Options;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class TwoFactorService(
    UserManager<ApplicationUser> userManager,
    IQrCodeGenerator qrCodeGenerator,
    AccountEmailSender emails,
    ISecurityEventLog securityLog,
    IOptions<TwoFactorOptions> twoFactorOptions)
{
    private const int RecoveryCodeCount = 10;

    public async Task<Result<TwoFactorStatusResponse>> GetStatusAsync(UserId userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }

        return new TwoFactorStatusResponse(
            await userManager.GetTwoFactorEnabledAsync(user),
            await userManager.CountRecoveryCodesAsync(user));
    }

    public async Task<Result<AuthenticatorSetupResponse>> SetupAuthenticatorAsync(UserId userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }
        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            return TwoFactorErrors.AlreadyEnabled;
        }

        await userManager.ResetAuthenticatorKeyAsync(user);
        var key = (await userManager.GetAuthenticatorKeyAsync(user))!;
        var uri = BuildAuthenticatorUri(user.Email!, key);

        return new AuthenticatorSetupResponse(FormatKey(key), uri, qrCodeGenerator.GeneratePngDataUrl(uri));
    }

    public async Task<Result<RecoveryCodesResponse>> EnableAsync(UserId userId, EnableTwoFactorRequest enable, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }
        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            return TwoFactorErrors.AlreadyEnabled;
        }
        if (await userManager.GetAuthenticatorKeyAsync(user) is null)
        {
            return TwoFactorErrors.NotConfigured;
        }

        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, AuthService.NormalizeCode(enable.Code));
        if (!valid)
        {
            return TwoFactorErrors.InvalidCode;
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await securityLog.RecordAsync(SecurityEventType.TwoFactorEnabled, user.Id, ct: ct);
        await emails.SendTwoFactorChangedNotificationAsync(user, enabled: true, ct);

        return new RecoveryCodesResponse(recoveryCodes!.ToList());
    }

    public async Task<Result<MessageResponse>> DisableAsync(UserId userId, PasswordRequest confirmation, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }
        if (!await userManager.CheckPasswordAsync(user, confirmation.Password))
        {
            return AccountErrors.InvalidPassword;
        }
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return TwoFactorErrors.NotEnabled;
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await securityLog.RecordAsync(SecurityEventType.TwoFactorDisabled, user.Id, ct: ct);
        await emails.SendTwoFactorChangedNotificationAsync(user, enabled: false, ct);

        return new MessageResponse("Uwierzytelnianie dwuskładnikowe zostało wyłączone.");
    }

    public async Task<Result<RecoveryCodesResponse>> RegenerateRecoveryCodesAsync(
        UserId userId, PasswordRequest confirmation, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountErrors.NotFound;
        }
        if (!await userManager.CheckPasswordAsync(user, confirmation.Password))
        {
            return AccountErrors.InvalidPassword;
        }
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return TwoFactorErrors.NotEnabled;
        }

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await securityLog.RecordAsync(SecurityEventType.RecoveryCodesRegenerated, user.Id, ct: ct);
        return new RecoveryCodesResponse(recoveryCodes!.ToList());
    }

    private string BuildAuthenticatorUri(string email, string key)
    {
        var issuer = Uri.EscapeDataString(twoFactorOptions.Value.AuthenticatorIssuer);
        return $"otpauth://totp/{issuer}:{Uri.EscapeDataString(email)}?secret={key}&issuer={issuer}&digits=6";
    }

    private static string FormatKey(string key) =>
        string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(chunk => new string(chunk)));
}
