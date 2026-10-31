using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const string EmailType = "email";
    public const string IntegrationEventType = "integration-event";

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Type { get; init; }

    public string? Name { get; init; }

    public required string Payload { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(m => m.Type).HasMaxLength(32);
        builder.Property(m => m.Name).HasMaxLength(128);
        builder.Property(m => m.Payload).HasColumnType("jsonb");
        builder.Property(m => m.LastError).HasMaxLength(2000);

        builder.HasIndex(m => m.NextAttemptAt).HasFilter("\"ProcessedAt\" IS NULL");
        builder.HasIndex(m => m.ProcessedAt);
    }
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(typeof(TimeSpan), "00:00:00.100", "00:10:00")]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 10;
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; set; } = true;
}
