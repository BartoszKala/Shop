using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Persistence.Configurations;

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.HasIndex(e => new { e.UserId, e.OccurredAt });
        builder.HasIndex(e => e.OccurredAt);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(64);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(SecurityEvent.UserAgentMaxLength);
        builder.Property(e => e.Details).HasMaxLength(SecurityEvent.DetailsMaxLength);
    }
}

internal sealed class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
{
    public void Configure(EntityTypeBuilder<UserConsent> builder)
    {
        builder.HasIndex(c => new { c.UserId, c.RecordedAt });

        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.DocumentVersion).HasMaxLength(UserConsent.VersionMaxLength);
        builder.Property(c => c.IpAddress).HasMaxLength(64);

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.Consents)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
