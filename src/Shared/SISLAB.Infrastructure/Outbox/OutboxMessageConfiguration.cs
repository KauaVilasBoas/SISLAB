using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Configuração EF Core para a entidade <see cref="OutboxMessage"/>.
/// Aplica convenções de naming snake_case (herdadas do SislabDbContextBase) e
/// define índices para otimizar a consulta do dispatcher:
/// - ix_outbox_messages_processed_at_utc: filtra mensagens pendentes (IS NULL).
/// - ix_outbox_messages_occurred_on_utc: ordenação temporal para processamento FIFO.
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

        // Índice principal do dispatcher: busca mensagens pendentes ordenadas por data.
        builder.HasIndex(m => m.ProcessedAtUtc)
            .HasDatabaseName("ix_outbox_messages_processed_at_utc");

        builder.HasIndex(m => m.OccurredOnUtc)
            .HasDatabaseName("ix_outbox_messages_occurred_on_utc");
    }
}
