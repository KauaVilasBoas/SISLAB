using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.Modules.Inventory.Domain.ValueObjects;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Partner"/> aggregate.
/// </summary>
/// <remarks>
/// Value-object mapping decisions (card [E3] #28):
/// <list type="bullet">
///   <item><see cref="Email"/> → a single nullable <c>contact_email</c> column via a value converter;
///   a null column reconstitutes as a null <see cref="Email"/> (no known contact). The address
///   round-trips through the validated <see cref="Email.FromValue"/> factory.</item>
///   <item><see cref="SampleNote"/> collection → owned into a child table <c>partner_samples</c>
///   (a real one-to-many owned by the aggregate). The reference is the natural business key within a
///   partner, so it pairs with the owner id as the composite primary key.</item>
/// </list>
/// </remarks>
internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("partners");

        builder.HasKey(partner => partner.Id);
        builder.Property(partner => partner.Id).ValueGeneratedNever();

        builder.Property(partner => partner.CompanyId)
            .IsRequired();

        builder.Property(partner => partner.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(partner => partner.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(partner => partner.Document)
            .HasMaxLength(40);

        // Contact e-mail: nullable single column. Null column => null Email (no known contact).
        builder.Property(partner => partner.ContactEmail)
            .HasColumnName("contact_email")
            .HasConversion(
                email => email == null ? null : email.Value,
                value => Email.FromValue(value))
            .HasMaxLength(254);

        builder.Property(partner => partner.Description)
            .HasMaxLength(1000);

        builder.Property(partner => partner.IsActive)
            .IsRequired();

        // Samples: owned one-to-many child table. The aggregate mutates the collection through its
        // methods; EF persists it via the private backing field.
        builder.OwnsMany(partner => partner.Samples, sample =>
        {
            sample.ToTable("partner_samples");

            sample.WithOwner().HasForeignKey("partner_id");

            sample.Property(s => s.Reference)
                .HasColumnName("reference")
                .IsRequired()
                .HasMaxLength(120);

            sample.Property(s => s.Status)
                .HasColumnName("status")
                .HasMaxLength(60);

            // (partner_id, reference) is the natural composite key: a reference is unique per partner.
            sample.HasKey("partner_id", nameof(SampleNote.Reference));
        });

        // The samples collection is populated through the aggregate's methods and read via the field.
        builder.Navigation(partner => partner.Samples)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Tenant isolation: index by (company_id, id). company_id NOT NULL is enforced above.
        builder.HasIndex(partner => new { partner.CompanyId, partner.Id })
            .HasDatabaseName("ix_partners_company_id_id");
    }
}
