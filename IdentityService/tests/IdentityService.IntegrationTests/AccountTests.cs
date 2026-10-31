using System.Net;
using System.Net.Http.Json;
using IdentityService.IntegrationTests.Infrastructure;

namespace IdentityService.IntegrationTests;

public sealed class AccountTests(IdentityApiFactory factory)
{
    [Fact]
    public async Task Two_factor_login_with_authenticator_and_recovery_code()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        client.Authorize(await client.LoginAsync(user.Email, user.Password));

        var setup = await (await client.PostAsync("/api/account/2fa/authenticator", null)).ReadJsonAsync();
        var key = setup.GetProperty("sharedKey").GetString()!;
        Assert.StartsWith("data:image/png;base64,", setup.GetProperty("qrCodeImage").GetString());

        var wrongCode = await client.PostAsJsonAsync("/api/account/2fa/enable", new { code = "000000" });
        await wrongCode.EnsureProblemAsync(HttpStatusCode.BadRequest, "two_factor.invalid_code");

        var enabled = await client.PostAsJsonAsync("/api/account/2fa/enable", new { code = TestApi.Totp(key) });
        await enabled.EnsureStatusAsync(HttpStatusCode.OK);
        var recoveryCodes = (await enabled.ReadJsonAsync()).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToList();
        Assert.Equal(10, recoveryCodes.Count);
        await factory.Mailbox.WaitForAsync(user.Email, "Włączono uwierzytelnianie dwuskładnikowe");

        var anonymous = factory.CreateDevice();
        var step1 = await (await anonymous.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password })).ReadJsonAsync();
        Assert.True(step1.GetProperty("requiresTwoFactor").GetBoolean());
        var twoFactorToken = step1.GetProperty("twoFactorToken").GetString();

        var badTotp = await anonymous.PostAsJsonAsync("/api/auth/login/2fa", new { twoFactorToken, code = "123456" });
        await badTotp.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_two_factor_code");

        var step2 = await anonymous.PostAsJsonAsync("/api/auth/login/2fa", new { twoFactorToken, code = TestApi.Totp(key) });
        await step2.EnsureStatusAsync(HttpStatusCode.OK);

        var forged = await anonymous.PostAsJsonAsync("/api/auth/login/2fa", new { twoFactorToken = "forged", code = TestApi.Totp(key) });
        await forged.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.two_factor_session_expired");

        var again = await (await anonymous.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password })).ReadJsonAsync();
        var recovery = await anonymous.PostAsJsonAsync("/api/auth/login/recovery-code",
            new { twoFactorToken = again.GetProperty("twoFactorToken").GetString(), recoveryCode = recoveryCodes[0] });
        await recovery.EnsureStatusAsync(HttpStatusCode.OK);

        var status = await (await client.GetAsync("/api/account/2fa")).ReadJsonAsync();
        Assert.True(status.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(9, status.GetProperty("recoveryCodesLeft").GetInt32());
    }

    [Fact]
    public async Task Changing_password_ends_other_sessions()
    {
        var laptop = factory.CreateDevice("Laptop");
        var phone = factory.CreateDevice("Phone");
        var user = await factory.CreateConfirmedUserAsync(laptop);
        laptop.Authorize(await laptop.LoginAsync(user.Email, user.Password));
        var phoneTokens = await phone.LoginAsync(user.Email, user.Password);

        var changed = await laptop.PostAsJsonAsync("/api/account/change-password",
            new { currentPassword = user.Password, newPassword = "ZmienioneHaslo1" });
        await changed.EnsureStatusAsync(HttpStatusCode.OK);
        var laptopTokens = TestApi.ReadTokens(await changed.ReadJsonAsync());

        var phoneRefresh = await phone.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = phoneTokens.RefreshToken });
        await phoneRefresh.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
        (await laptop.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = laptopTokens.RefreshToken })).EnsureSuccessStatusCode();
        await factory.Mailbox.WaitForAsync(user.Email, "Hasło zostało zmienione");

        var wrongCurrent = await laptop.Authorize(laptopTokens).PostAsJsonAsync("/api/account/change-password",
            new { currentPassword = "Zle12345", newPassword = "JeszczeInne1" });
        await wrongCurrent.EnsureProblemAsync(HttpStatusCode.BadRequest, "account.invalid_password");
    }

    [Fact]
    public async Task Changing_email_requires_confirmation_from_the_new_address()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        client.Authorize(await client.LoginAsync(user.Email, user.Password));
        var newEmail = TestApi.UniqueEmail("new");

        (await client.PostAsJsonAsync("/api/account/change-email", new { newEmail, password = user.Password }))
            .EnsureSuccessStatusCode();
        var link = FakeMailbox.LinkParameters(await factory.Mailbox.WaitForAsync(newEmail, "Potwierdź zmianę adresu e-mail"));

        var confirmed = await client.PostAsJsonAsync("/api/account/confirm-email-change",
            new { userId = link["userId"], email = link["email"], code = link["code"] });
        await confirmed.EnsureStatusAsync(HttpStatusCode.OK);

        await factory.Mailbox.WaitForAsync(user.Email, "Adres e-mail konta został zmieniony");
        var changedEvent = await factory.IntegrationEvents.WaitForAsync("user.email_changed", user.Id);
        Assert.Equal(newEmail, changedEvent.GetProperty("newEmail").GetString());
        await factory.CreateDevice().LoginAsync(newEmail, user.Password);
    }

    [Fact]
    public async Task Sessions_can_be_listed_and_revoked_and_new_devices_are_reported()
    {
        var laptop = factory.CreateDevice("Laptop/1.0");
        var phone = factory.CreateDevice("Phone/1.0");
        var user = await factory.CreateConfirmedUserAsync(laptop);
        laptop.Authorize(await laptop.LoginAsync(user.Email, user.Password));
        var phoneTokens = await phone.LoginAsync(user.Email, user.Password);

        await factory.Mailbox.WaitForAsync(user.Email, "Nowe logowanie na Twoje konto");

        var sessions = (await (await laptop.GetAsync("/api/account/sessions")).ReadJsonAsync()).EnumerateArray().ToList();
        Assert.Equal(2, sessions.Count);
        var current = Assert.Single(sessions, s => s.GetProperty("isCurrent").GetBoolean());
        Assert.Equal("Laptop/1.0", current.GetProperty("userAgent").GetString());

        var phoneSession = sessions.Single(s => !s.GetProperty("isCurrent").GetBoolean()).GetProperty("id").GetGuid();
        (await laptop.DeleteAsync($"/api/account/sessions/{phoneSession}")).EnsureSuccessStatusCode();

        var phoneRefresh = await phone.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = phoneTokens.RefreshToken });
        await phoneRefresh.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");

        var events = await (await laptop.GetAsync("/api/account/security-events?pageSize=50")).ReadJsonAsync();
        var types = events.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToList();
        Assert.Contains("Registered", types);
        Assert.Contains("LoginSucceeded", types);
        Assert.Contains("SessionRevoked", types);
    }

    [Fact]
    public async Task Profile_name_can_be_updated_and_is_validated()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        client.Authorize(await client.LoginAsync(user.Email, user.Password));

        var updated = await client.PutAsJsonAsync("/api/account/me", new { firstName = " Ewa ", lastName = "Nowak" });
        await updated.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Ewa", (await updated.ReadJsonAsync()).GetProperty("firstName").GetString());

        var tooLong = await client.PutAsJsonAsync("/api/account/me", new { firstName = new string('x', 101) });
        await tooLong.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }
}
