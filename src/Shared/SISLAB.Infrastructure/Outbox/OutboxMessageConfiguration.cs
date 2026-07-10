using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// EF Core configuration for <see cref="OutboxMessage"/>.
/// Indexes optimize the dispatcher query:
/// - ix_outbox_messages_processed_at_utc: filters pending messages (IS NULL).
/// - ix_outbox_messages_occurred_on_utc: temporal ordering for FIFO processing.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.OccurredOnUtc)
            .IsRequired();

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.Property(m => m.ProcessedAtUtc)
            .IsRequired(false);

        builder.Property(m => m.Error)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.HasIndex(m => m.ProcessedAtUtc)
            .HasDatabaseName("ix_outbox_messages_processed_at_utc");

        builder.HasIndex(m => m.OccurredOnUtc)
            .HasDatabaseName("ix_outbox_messages_occurred_on_utc");
    }
}
