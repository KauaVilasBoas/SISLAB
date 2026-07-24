using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Attachment"/> aggregate (SISLAB-09), table
/// <c>experiments.attachments</c>. The <see cref="AttachmentTarget"/> value object is table-split into two columns
/// (<c>target_kind</c> + <c>target_id</c>) and the <see cref="StoredFileKey"/> into a single opaque
/// <c>storage_key</c> column — the domain keeps only that key, never a path or URL.
/// </summary>
internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).ValueGeneratedNever();

        builder.Property(attachment => attachment.CompanyId).IsRequired();
        builder.Property(attachment => attachment.AnimalId).IsRequired();

        // Storage key value object → single opaque column. Owned types name their columns explicitly in snake_case
        // because the base context skips the snake_case pass for owned types.
        builder.OwnsOne(attachment => attachment.StorageKey, key =>
        {
            key.Property(k => k.Value)
                .HasColumnName("storage_key").HasMaxLength(300).IsRequired();
        });
        builder.Navigation(attachment => attachment.StorageKey).IsRequired();

        // Target value object → (kind, id) table-split into the attachment row.
        builder.OwnsOne(attachment => attachment.Target, target =>
        {
            target.Property(t => t.Kind)
                .HasColumnName("target_kind").HasConversion<string>().HasMaxLength(30).IsRequired();
            target.Property(t => t.TargetId)
                .HasColumnName("target_id").IsRequired();
        });
        builder.Navigation(attachment => attachment.Target).IsRequired();

        builder.Property(attachment => attachment.FileName).IsRequired().HasMaxLength(260);
        builder.Property(attachment => attachment.ContentType).IsRequired().HasMaxLength(120);
        builder.Property(attachment => attachment.SizeBytes).IsRequired();
        builder.Property(attachment => attachment.Origin).HasMaxLength(120);
        builder.Property(attachment => attachment.UploadedBy).IsRequired().HasMaxLength(200);
        builder.Property(attachment => attachment.UploadedAtUtc).IsRequired();

        // Tenant isolation access path + the read-side's animal/target lookup path.
        builder.HasIndex(attachment => new { attachment.CompanyId, attachment.Id })
            .HasDatabaseName("ix_attachments_company_id_id");
        builder.HasIndex(attachment => attachment.AnimalId)
            .HasDatabaseName("ix_attachments_animal_id");
    }
}
