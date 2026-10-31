using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityService.IntegrationTests.Infrastructure;

namespace IdentityService.IntegrationTests;

public sealed class AdminTests(IdentityApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateDevice("AdminPanel");
        return admin.Authorize(await admin.LoginAsync(IdentityApiFactory.AdminEmail, IdentityApiFactory.AdminPassword));
    }

    [Fact]
    public async Task Regular_users_cannot_use_admin_endpoints()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        client.Authorize(await client.LoginAsync(user.Email, user.Password));

        await (await client.GetAsync("/api/admin/users")).EnsureStatusAsync(HttpStatusCode.Forbidden);
        await (await factory.CreateClient().GetAsync("/api/admin/users")).EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_can_search_users_and_see_details()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        var admin = await AdminAsync();

        var page = await (await admin.GetAsync($"/api/admin/users?search={Uri.EscapeDataString(user.Email[..20])}")).ReadJsonAsync();
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(user.Id, item.GetProperty("id").GetGuid());

        var details = await (await admin.GetAsync($"/api/admin/users/{user.Id}")).ReadJsonAsync();
        Assert.Equal(user.Email, details.GetProperty("email").GetString());
        Assert.True(details.GetProperty("hasPassword").GetBoolean());

        await (await admin.GetAsync($"/api/admin/users/{Guid.NewGuid()}")).EnsureProblemAsync(HttpStatusCode.NotFound, "admin.user_not_found");
    }

    [Fact]
    public async Task Locking_blocks_login_and_ends_sessions_until_unlocked()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        var tokens = await client.LoginAsync(user.Email, user.Password);
        var admin = await AdminAsync();

        (await admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/lock", new { reason = "spam" })).EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });
        await login.EnsureProblemAsync(HttpStatusCode.Forbidden, "auth.locked_out");
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        await refresh.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");

        (await admin.PostAsync($"/api/admin/users/{user.Id}/unlock", null)).EnsureSuccessStatusCode();
        await client.LoginAsync(user.Email, user.Password);

        var until = new DateTimeOffset(DateTime.UtcNow.AddDays(1).Ticks, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(1));
        var temporary = await admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/lock", new { until });
        await temporary.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.NotEqual(JsonValueKind.Null, (await temporary.ReadJsonAsync()).GetProperty("lockedOutUntil").ValueKind);

        var events = await (await admin.GetAsync($"/api/admin/users/{user.Id}/security-events")).ReadJsonAsync();
        var lockEvent = events.GetProperty("items").EnumerateArray().First(e => e.GetProperty("type").GetString() == "LockedByAdmin");
        Assert.True(lockEvent.GetProperty("performedByAnotherUser").GetBoolean());
    }

    [Fact]
    public async Task Admin_cannot_lock_themselves_or_drop_their_own_admin_role()
    {
        var admin = await AdminAsync();
        var me = await (await admin.GetAsync("/api/account/me")).ReadJsonAsync();
        var adminId = me.GetProperty("id").GetGuid();

        await (await admin.PostAsJsonAsync($"/api/admin/users/{adminId}/lock", new { }))
            .EnsureProblemAsync(HttpStatusCode.BadRequest, "admin.cannot_modify_self");
        await (await admin.PutAsJsonAsync($"/api/admin/users/{adminId}/roles", new { roles = new[] { "User" } }))
            .EnsureProblemAsync(HttpStatusCode.BadRequest, "admin.cannot_modify_self");
    }

    [Fact]
    public async Task Roles_set_by_admin_appear_in_the_next_token()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        var tokens = await client.LoginAsync(user.Email, user.Password);
        var admin = await AdminAsync();

        var invalid = await admin.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles", new { roles = new[] { "SuperAdmin" } });
        await invalid.EnsureStatusAsync(HttpStatusCode.BadRequest);

        (await admin.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles", new { roles = new[] { "User", "Admin" } }))
            .EnsureSuccessStatusCode();

        var refreshed = TestApi.ReadTokens(await (await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = tokens.RefreshToken })).ReadJsonAsync());
        await (await client.Authorize(refreshed).GetAsync("/api/admin/users")).EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Forced_password_reset_invalidates_the_password_and_sends_a_link()
    {
        var client = factory.CreateDevice();
        var user = await factory.CreateConfirmedUserAsync(client);
        var admin = await AdminAsync();

        (await admin.PostAsync($"/api/admin/users/{user.Id}/force-password-reset", null)).EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });
        await login.EnsureProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        var link = FakeMailbox.LinkParameters(await factory.Mailbox.WaitForAsync(user.Email, "Ustaw nowe hasło"));
        (await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = link["email"], code = link["code"], newPassword = "PoResecie123" })).EnsureSuccessStatusCode();
        await client.LoginAsync(user.Email, "PoResecie123");
    }
}
