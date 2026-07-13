using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.ReferenceRanges;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ReferenceRange"/> aggregate (schema <c>configuration</c>).
/// </summary>
/// <remarks>
/// <b>Bounds → two nullable columns.</b> <see cref="RangeBounds"/> is mapped as an owned single instance
/// onto <c>minimum</c> / <c>maximum</c>; both are nullable to model an open-ended bound, and the "min ≤ max,
/// at least one bound" invariant is reconstituted through the validated factory on read-back. The range is
/// unique per <c>(company, analyte, species)</c>.
/// </remarks>
internal sealed class ReferenceRangeConfiguration : IEntityTypeConfiguration<ReferenceRange>
{
    public void Configure(EntityTypeBuilder<ReferenceRange> builder)
    {
        builder.ToTable("reference_ranges");

        builder.HasKey(range => range.Id);
        builder.Property(range => range.Id).ValueGeneratedNever();

        builder.Property(range => range.CompanyId).IsRequired();

        builder.Property(range => range.Analyte)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(range => range.Species)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(range => range.Unit)
            .HasMaxLength(20);

        // Bounds value object: two nullable numeric columns kept together via an owned single instance,
        // rebuilt through the validated RangeBounds.Of factory (so an inverted persisted interval fails fast).
        builder.OwnsOne(range => range.Bounds, bounds =>
        {
            bounds.Property(b => b.Minimum)
                .HasColumnName("minimum")
                .HasColumnType("numeric(18,4)");

            bounds.Property(b => b.Maximum)
                .HasColumnName("maximum")
                .HasColumnType("numeric(18,4)");
        });
        builder.Navigation(range => range.Bounds).IsRequired();

        // A tenant has one range per (analyte, species): the natural key is unique per company.
        builder.HasIndex(range => new { range.CompanyId, range.Analyte, range.Species })
            .IsUnique()
            .HasDatabaseName("ux_reference_ranges_company_id_analyte_species");

        // Tenant isolation access path.
        builder.HasIndex(range => new { range.CompanyId, range.Id })
            .HasDatabaseName("ix_reference_ranges_company_id_id");
    }
}
