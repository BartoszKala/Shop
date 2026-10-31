using System.Security.Cryptography;
using System.Text;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Identity;

public sealed class PasswordPolicyOptions
{
    public const string SectionName = "PasswordPolicy";

    public bool CheckBreachedPasswords { get; set; } = true;
}

internal sealed class PwnedPasswordValidator(
    HttpClient httpClient,
    IOptions<PasswordPolicyOptions> options,
    ILogger<PwnedPasswordValidator> logger) : IPasswordValidator<ApplicationUser>
{
    public const string ErrorCode = "PasswordBreached";

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (!options.Value.CheckBreachedPasswords || string.IsNullOrEmpty(password))
        {
            return IdentityResult.Success;
        }

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        var (prefix, suffix) = (hash[..5], hash[5..]);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{prefix}");
            request.Headers.Add("Add-Padding", "true");
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(':');
                if (parts.Length == 2 && parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(parts[1], out var count) && count > 0)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = ErrorCode,
                        Description = "To hasło pojawiło się w wycieku danych i nie jest bezpieczne. Wybierz inne."
                    });
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Breached-password check skipped: Pwned Passwords API unavailable");
        }

        return IdentityResult.Success;
    }
}
