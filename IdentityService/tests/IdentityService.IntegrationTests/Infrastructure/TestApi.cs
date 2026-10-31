using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdentityService.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string Email, string Password);

public sealed record Tokens(string AccessToken, string RefreshToken);

public static class TestApi
{
    public const string DefaultPassword = "Haslo12345";

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@shop.test";

    public static async Task<HttpResponseMessage> RegisterAsync(
        this HttpClient client, string email, string password = DefaultPassword, bool acceptTerms = true, bool marketing = false) =>
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            firstName = "Anna",
            lastName = "Testowa",
            acceptTerms,
            marketingConsent = marketing
        });

    public static async Task<TestUser> CreateConfirmedUserAsync(
        this IdentityApiFactory factory, HttpClient client, bool marketing = false)
    {
        var email = UniqueEmail();
        (await client.RegisterAsync(email, marketing: marketing)).EnsureSuccessStatusCode();

        var mail = await factory.Mailbox.WaitForAsync(email, "Potwierdź swój adres e-mail");
        var link = FakeMailbox.LinkParameters(mail);
        (await client.PostAsJsonAsync("/api/auth/confirm-email", new { userId = link["userId"], code = link["code"] }))
            .EnsureSuccessStatusCode();

        return new TestUser(Guid.Parse(link["userId"]), email, DefaultPassword);
    }

    public static async Task<Tokens> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        await response.EnsureStatusAsync(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        Assert.False(body.GetProperty("requiresTwoFactor").GetBoolean(), "Expected a login without 2FA.");
        return ReadTokens(body.GetProperty("tokens"));
    }

    public static Tokens ReadTokens(JsonElement tokens) =>
        new(tokens.GetProperty("accessToken").GetString()!, tokens.GetProperty("refreshToken").GetString()!);

    public static HttpClient Authorize(this HttpClient client, Tokens tokens)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    public static async Task EnsureStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            Assert.Fail($"Expected {(int)expected} {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public static async Task EnsureProblemAsync(this HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        await response.EnsureStatusAsync(expected);
        var body = await response.ReadJsonAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    public static string Totp(string sharedKey)
    {
        var key = Base32Decode(sharedKey.Replace(" ", string.Empty).ToUpperInvariant());
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = new StringBuilder();
        foreach (var c in input.TrimEnd('='))
        {
            bits.Append(Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0'));
        }

        var bytes = new byte[bits.Length / 8];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(bits.ToString(i * 8, 8), 2);
        }
        return bytes;
    }
}
