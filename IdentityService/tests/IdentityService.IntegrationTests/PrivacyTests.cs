using System.Net;
using System.Net.Http.Json;
using IdentityService.IntegrationTests.Infrastructure;

namespace IdentityService.IntegrationTests;

public sealed class PrivacyTests(IdentityApiFactory factory)
{
    [Fact]
    public async Task Consents_are_recorded_at_registration_and_marketing_can_be_withdrawn()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client, marketing: true);
        client.Authorize(await client.LoginAsync(user.Email, user.Password));

        var consents = await (await client.GetAsync("/api/account/privacy/consents")).ReadJsonAsync();
        Assert.True(consents.GetProperty("marketingConsent").GetBoolean());
        Assert.Equal(consents.GetProperty("currentTermsVersion").GetString(), consents.GetProperty("acceptedTermsVersion").GetString());
        Assert.Equal(3, consents.GetProperty("history").GetArrayLength());

        var withdrawn = await client.PutAsJsonAsync("/api/account/privacy/consents/marketing", new { granted = false });
        await withdrawn.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False((await withdrawn.ReadJsonAsync()).GetProperty("marketingConsent").GetBoolean());

        var changed = await factory.IntegrationEvents.WaitForAsync("user.marketing_consent_changed", user.Id);
        Assert.False(changed.GetProperty("granted").GetBoolean());
    }

    [Fact]
    public async Task Export_contains_profile_consents_sessions_and_security_events()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        client.Authorize(await client.LoginAsync(user.Email, user.Password));

        var response = await client.GetAsync("/api/account/privacy/export");

        await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        var export = await response.ReadJsonAsync();
        Assert.Equal(user.Email, export.GetProperty("profile").GetProperty("email").GetString());
        Assert.Equal(2, export.GetProperty("consents").GetArrayLength());
        Assert.Equal(1, export.GetProperty("activeSessions").GetArrayLength());
        Assert.True(export.GetProperty("securityEvents").GetArrayLength() >= 3);
    }

    [Fact]
    public async Task Deleting_the_account_removes_it_and_notifies_other_services()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        var tokens = await client.LoginAsync(user.Email, user.Password);
        client.Authorize(tokens);

        var wrongPassword = await client.PostAsJsonAsync("/api/account/privacy/delete-account", new { password = "Zle12345" });
        await wrongPassword.EnsureProblemAsync(HttpStatusCode.BadRequest, "account.invalid_password");

        var deleted = await client.PostAsJsonAsync("/api/account/privacy/delete-account", new { password = user.Password });
        await deleted.EnsureStatusAsync(HttpStatusCode.OK);

        await factory.IntegrationEvents.WaitForAsync("user.deleted", user.Id);
        await factory.Mailbox.WaitForAsync(user.Email, "Konto zostało usunięte");

        var login = await factory.CreateDevice().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });
        await login.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        await refresh.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");

        (await client.RegisterAsync(user.Email)).EnsureSuccessStatusCode();
    }
}
