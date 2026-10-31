using System.Net;
using System.Net.Http.Json;
using IdentityService.IntegrationTests.Infrastructure;

namespace IdentityService.IntegrationTests;

public sealed class AuthTests(IdentityApiFactory factory)
{
    [Fact]
    public async Task Registration_requires_accepting_the_terms()
    {
        var client = factory.CreateDevice();

        var response = await client.RegisterAsync(TestApi.UniqueEmail(), acceptTerms: false);

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.True((await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("AcceptTerms", out _));
    }

    [Fact]
    public async Task Register_confirm_login_and_read_profile()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client, marketing: true);

        var registered = await factory.IntegrationEvents.WaitForAsync("user.registered", user.Id);
        Assert.Equal(user.Email, registered.GetProperty("email").GetString());

        var tokens = await client.LoginAsync(user.Email, user.Password);
        var profile = await (await client.Authorize(tokens).GetAsync("/api/account/me")).ReadJsonAsync();

        Assert.Equal(user.Id, profile.GetProperty("id").GetGuid());
        Assert.Equal(["User"], profile.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.True(profile.GetProperty("marketingConsent").GetBoolean());
        Assert.False(profile.GetProperty("requiresTermsAcceptance").GetBoolean());
    }

    [Fact]
    public async Task Duplicate_registration_reports_taken_email()
    {
        var client = factory.CreateDevice();
        var email = TestApi.UniqueEmail();
        (await client.RegisterAsync(email)).EnsureSuccessStatusCode();

        var response = await client.RegisterAsync(email);

        await response.EnsureProblemAsync(HttpStatusCode.BadRequest, "validation");
        Assert.True((await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("DuplicateEmail", out _));
    }

    [Fact]
    public async Task Unconfirmed_account_cannot_log_in_but_wrong_password_does_not_reveal_it()
    {
        var client = factory.CreateDevice();
        var email = TestApi.UniqueEmail();
        (await client.RegisterAsync(email)).EnsureSuccessStatusCode();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Zle12345" });
        await wrongPassword.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        var rightPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestApi.DefaultPassword });
        await rightPassword.EnsureProblemAsync(HttpStatusCode.Forbidden, "auth.email_not_confirmed");
    }

    [Fact]
    public async Task Account_is_locked_after_five_failed_logins()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);

        for (var i = 0; i < 4; i++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Zle12345" });
            await failed.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        }

        var fifth = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Zle12345" });
        await fifth.EnsureProblemAsync(HttpStatusCode.Forbidden, "auth.locked_out");

        var correct = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });
        await correct.EnsureProblemAsync(HttpStatusCode.Forbidden, "auth.locked_out");
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_reuse_ends_all_sessions()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        var first = await client.LoginAsync(user.Email, user.Password);

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        await refreshed.EnsureStatusAsync(HttpStatusCode.OK);
        var second = TestApi.ReadTokens(await refreshed.ReadJsonAsync());

        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        await replay.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
        var afterReplay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken });
        await afterReplay.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        var tokens = await client.LoginAsync(user.Email, user.Password);

        (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = tokens.RefreshToken }))
            .EnsureSuccessStatusCode();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        await refresh.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
    }

    [Fact]
    public async Task Forgotten_password_can_be_reset_through_the_e_mail_link()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email })).EnsureSuccessStatusCode();
        var link = FakeMailbox.LinkParameters(await factory.Mailbox.WaitForAsync(user.Email, "Ustaw nowe hasło"));

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = link["email"], code = link["code"], newPassword = "NoweHaslo123" });
        await reset.EnsureStatusAsync(HttpStatusCode.OK);

        await client.LoginAsync(user.Email, "NoweHaslo123");
        var reused = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = link["email"], code = link["code"], newPassword = "InneHaslo123" });
        await reused.EnsureProblemAsync(HttpStatusCode.BadRequest, "auth.invalid_link");
    }
}
