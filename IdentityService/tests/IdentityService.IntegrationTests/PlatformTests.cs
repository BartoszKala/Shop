using System.Net;
using IdentityService.IntegrationTests.Infrastructure;

namespace IdentityService.IntegrationTests;

public sealed class PlatformTests(IdentityApiFactory factory)
{
    [Fact]
    public async Task Health_endpoints_report_healthy_including_the_database()
    {
        var client = factory.CreateClient();

        await (await client.GetAsync("/alive")).EnsureStatusAsync(HttpStatusCode.OK);
        await (await client.GetAsync("/health")).EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Discovery_document_and_jwks_are_published()
    {
        var client = factory.CreateClient();

        var configuration = await (await client.GetAsync("/.well-known/openid-configuration")).ReadJsonAsync();
        Assert.EndsWith("/.well-known/jwks.json", configuration.GetProperty("jwks_uri").GetString());

        var jwks = await (await client.GetAsync("/.well-known/jwks.json")).ReadJsonAsync();
        var key = Assert.Single(jwks.GetProperty("keys").EnumerateArray());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.False(key.TryGetProperty("d", out _), "Private key material must never be published.");
    }

    [Fact]
    public async Task Protected_endpoints_reject_anonymous_and_forged_tokens()
    {
        var client = factory.CreateClient();
        await (await client.GetAsync("/api/account/me")).EnsureStatusAsync(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new("Bearer", "eyJhbGciOiJub25lIn0.eyJzdWIiOiJ4In0.");
        await (await client.GetAsync("/api/account/me")).EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }
}
