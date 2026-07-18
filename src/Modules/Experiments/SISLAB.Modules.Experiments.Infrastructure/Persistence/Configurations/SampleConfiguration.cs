using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Experiments.Domain.Biobank;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Sample"/> aggregate and its owned analyses (card [E11] #89 — the
/// biobank, decision F4: its own aggregate, not a reuse of the Inventory stock item).
/// </summary>
/// <remarks>
/// Tables under the <c>experiments</c> schema: <c>samples</c> → <c>sample_analyses</c>. The
/// <see cref="SampleAmount"/> collected quantity and the optional <see cref="SISLAB.SharedKernel.Domain.TemperatureRange"/>
/// conservation range are table-split into the sample row; each analysis' consumed amount is table-split into the
/// analysis row. The derived <c>RemainingQuantity</c>/<c>ConsumedQuantity</c> are computed properties with no
/// setter, so EF never maps them — the balance is always recomputed from the analyses, never stored. Owned types
/// name their columns explicitly in snake_case because the base context skips the snake_case pass for owned types.
/// </remarks>
internal sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("samples");

        builder.HasKey(sample => sample.Id);
        builder.Property(sample => sample.Id).ValueGeneratedNever();

        builder.Property(sample => sample.CompanyId).IsRequired();

        builder.Property(sample => sample.Code).IsRequired().HasMaxLength(60);

        builder.Property(sample => sample.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(sample => sample.ProjectId).IsRequired();
        builder.Property(sample => sample.BatchId).IsRequired();
        builder.Property(sample => sample.AnimalId).IsRequired();
        builder.Property(sample => sample.SourceExperimentId).IsRequired();

        builder.Property(sample => sample.StorageLabel).HasMaxLength(120);
        builder.Property(sample => sample.Notes).HasMaxLength(2000);
        builder.Property(sample => sample.CollectedBy).IsRequired().HasMaxLength(200);
        builder.Property(sample => sample.CollectedAtUtc).IsRequired();

        // Collected quantity value object, table-split into the sample row.
        builder.OwnsOne(sample => sample.CollectedQuantity, amount =>
        {
            amount.Property(a => a.Value)
                .HasColumnName("collected_value").HasColumnType("numeric(18,4)").IsRequired();
            amount.Property(a => a.Unit)
                .HasColumnName("collected_unit").HasMaxLength(30).IsRequired();
        });
        builder.Navigation(sample => sample.CollectedQuantity).IsRequired();

        // Optional conservation temperature range, table-split into nullable columns on the sample row.
        builder.OwnsOne(sample => sample.ConservationRange, range =>
        {
            range.Property(r => r.MinimumCelsius)
                .HasColumnName("conservation_temp_min").HasColumnType("numeric(6,2)");
            range.Property(r => r.MaximumCelsius)
                .HasColumnName("conservation_temp_max").HasColumnType("numeric(6,2)");
        });

        builder.OwnsMany(sample => sample.Analyses, ConfigureAnalyses);
        builder.Navigation(sample => sample.Analyses).AutoInclude();

        // The derived balance is never persisted — it is recomputed from the analyses on every read.
        builder.Ignore(sample => sample.ConsumedQuantity);
        builder.Ignore(sample => sample.RemainingQuantity);
        builder.Ignore(sample => sample.IsDepleted);

        // Tenant isolation: index by (company_id, id); unique code per company.
        builder.HasIndex(sample => new { sample.CompanyId, sample.Id })
            .HasDatabaseName("ix_samples_company_id_id");
        builder.HasIndex(sample => new { sample.CompanyId, sample.Code })
            .IsUnique()
            .HasDatabaseName("ux_samples_company_id_code");
        builder.HasIndex(sample => sample.SourceExperimentId)
            .HasDatabaseName("ix_samples_source_experiment_id");
    }

    private static void ConfigureAnalyses(OwnedNavigationBuilder<Sample, Analysis> analyses)
    {
        analyses.ToTable("sample_analyses");

        analyses.WithOwner().HasForeignKey("sample_id");
        analyses.HasKey(analysis => analysis.Id);
        analyses.Property(analysis => analysis.Id).HasColumnName("id").ValueGeneratedNever();
        analyses.Property<Guid>("sample_id");

        analyses.Property(analysis => analysis.Name).HasColumnName("name").HasMaxLength(200).IsRequired();

        // Consumed amount value object, table-split into the analysis row (shares the sample's unit by invariant).
        analyses.OwnsOne(analysis => analysis.ConsumedAmount, amount =>
        {
            amount.Property(a => a.Value)
                .HasColumnName("consumed_value").HasColumnType("numeric(18,4)").IsRequired();
            amount.Property(a => a.Unit)
                .HasColumnName("consumed_unit").HasMaxLength(30).IsRequired();
        });
        analyses.Navigation(analysis => analysis.ConsumedAmount).IsRequired();

        analyses.Property(analysis => analysis.PerformedBy)
            .HasColumnName("performed_by").HasMaxLength(200).IsRequired();
        analyses.Property(analysis => analysis.PerformedAtUtc).HasColumnName("performed_at_utc").IsRequired();
        analyses.Property(analysis => analysis.Result).HasColumnName("result").HasMaxLength(4000);
        analyses.Property(analysis => analysis.Status)
            .HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();

        analyses.HasIndex("sample_id").HasDatabaseName("ix_sample_analyses_sample_id");
    }
}
