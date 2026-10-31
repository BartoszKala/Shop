using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Outbox;
using IdentityService.Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public class AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
    : IdentityUserContext<ApplicationUser, UserId>(options), IDataProtectionKeyContext
{
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppIdentityDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        var idTypes = typeof(UserId).Assembly.GetTypes()
            .Where(StronglyTypedIds.IsStronglyTypedId);

        foreach (var idType in idTypes)
        {
            configurationBuilder.Properties(idType)
                .HaveConversion(typeof(StronglyTypedIdValueConverter<>).MakeGenericType(idType));
        }
    }
}
