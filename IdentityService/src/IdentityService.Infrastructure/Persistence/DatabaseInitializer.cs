using IdentityService.Application.Abstractions;
using IdentityService.Application.Options;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public List<SeedUser> Users { get; set; } = [];

    public sealed class SeedUser
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<string> Roles { get; set; } = [];
    }
}

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        await provider.GetRequiredService<AppIdentityDbContext>().Database.MigrateAsync(ct);

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var userRoles = provider.GetRequiredService<IUserRoleRepository>();
        var consents = provider.GetRequiredService<IConsentRepository>();
        var legal = provider.GetRequiredService<IOptions<LegalOptions>>().Value;
        foreach (var seed in provider.GetRequiredService<IOptions<SeedOptions>>().Value.Users)
        {
            if (await userManager.FindByEmailAsync(seed.Email) is not null)
            {
                continue;
            }

            var created = ApplicationUser.Create(seed.Email, seed.FirstName, seed.LastName, DateTimeOffset.UtcNow);
            if (!created.IsSuccess)
            {
                throw SeedFailed(seed.Email, created.Error.Message);
            }

            var user = created.Value;
            user.EmailConfirmed = true;

            var result = await userManager.CreateAsync(user, seed.Password);
            if (!result.Succeeded)
            {
                throw SeedFailed(seed.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            foreach (var role in seed.Roles)
            {
                var roleAdded = await userRoles.AddToRoleAsync(user.Id, role, ct);
                if (!roleAdded.IsSuccess)
                {
                    throw SeedFailed(seed.Email, roleAdded.Error.Message);
                }
            }

            foreach (var (type, version) in new[]
                     {
                         (ConsentType.TermsOfService, legal.TermsOfServiceVersion),
                         (ConsentType.PrivacyPolicy, legal.PrivacyPolicyVersion)
                     })
            {
                var consent = UserConsent.Create(user.Id, type, true, version, DateTimeOffset.UtcNow, null);
                if (!consent.IsSuccess)
                {
                    throw SeedFailed(seed.Email, consent.Error.Message);
                }
                await consents.AddAsync(consent.Value, ct);
            }

            logger.LogInformation("Seeded user {Email} with roles {Roles}", seed.Email, seed.Roles);
        }
    }

    private static InvalidOperationException SeedFailed(string email, string reason) =>
        new($"Seeding user '{email}' failed: {reason}");
}
