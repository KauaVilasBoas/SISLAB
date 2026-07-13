using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Notifications.Domain.Notifications;

namespace SISLAB.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Notification"/> aggregate (schema <c>notifications</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Value objects → columns.</b> The two value objects are flattened onto the row rather than owned into
/// child tables, because each is a single conceptual value:
/// <list type="bullet">
///   <item><see cref="NotificationReference"/> → two columns (<c>reference_target_type</c> +
///   <c>reference_target_id</c>), reconstituted through the validated <c>NotificationReference.To</c>
///   factory. Mapped as an owned single instance so the two columns round-trip together.</item>
///   <item><see cref="DedupeKey"/> → one <c>dedupe_key</c> column via a value converter over the validated
///   <c>DedupeKey.FromValue</c> factory.</item>
/// </list>
/// </para>
/// <para>
/// <b>Idempotency invariant (card #64a).</b> The uniqueness rule is "one <em>active</em> (unread) notification
/// per (company, dedupe key)". It is expressed as a <b>partial</b> unique index filtered to
/// <c>is_read = false</c>: two unread rows with the same key in the same company cannot coexist (the write
/// path relies on this with <c>ON CONFLICT DO NOTHING</c>), yet once a notification is read the key is freed,
/// so the same alert can legitimately re-fire in a later cycle. The partial filter is what makes the natural
/// key reusable over time instead of a permanent lock.
/// </para>
/// </remarks>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <summary>The read-side and write-side agree on this exact predicate for the partial unique index.</summary>
    internal const string ActiveDedupeIndexName = "ux_notifications_company_id_dedupe_key_active";

    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();

        builder.Property(notification => notification.CompanyId)
            .IsRequired();

        builder.Property(notification => notification.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(notification => notification.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(notification => notification.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(notification => notification.Description)
            .IsRequired()
            .HasMaxLength(1000);

        // Reference value object: two columns kept together via an owned single instance. The nested build
        // uses the private ctor + validated factory on read-back (EF materializes owned types by field).
        builder.OwnsOne(notification => notification.Reference, reference =>
        {
            reference.Property(r => r.TargetType)
                .HasColumnName("reference_target_type")
                .IsRequired()
                .HasMaxLength(60);

            reference.Property(r => r.TargetId)
                .HasColumnName("reference_target_id")
                .IsRequired();
        });
        builder.Navigation(notification => notification.Reference).IsRequired();

        // Dedupe key: single column via value converter over the validated factory.
        builder.Property(notification => notification.DedupeKey)
            .HasColumnName("dedupe_key")
            .IsRequired()
            .HasMaxLength(200)
            .HasConversion(
                key => key.Value,
                value => DedupeKey.FromValue(value));

        builder.Property(notification => notification.IsRead)
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .IsRequired();

        builder.Property(notification => notification.ReadAtUtc);

        // Read access path for the bell: the company's notifications, newest first.
        builder.HasIndex(notification => new { notification.CompanyId, notification.CreatedAtUtc })
            .HasDatabaseName("ix_notifications_company_id_created_at_utc");

        // Idempotency: at most one ACTIVE (unread) notification per (company, dedupe key). Partial index so a
        // read notification frees the key for the next cycle. company_id is snake_cased by the base convention;
        // dedupe_key is named explicitly above. The filter uses the physical column name.
        builder.HasIndex(notification => new { notification.CompanyId, notification.DedupeKey })
            .IsUnique()
            .HasFilter("is_read = false")
            .HasDatabaseName(ActiveDedupeIndexName);
    }
}
