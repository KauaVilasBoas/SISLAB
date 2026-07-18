using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Experiment"/> aggregate and its TPH hierarchy (decision card #68).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>TPH.</b> The whole hierarchy maps to a single <c>experiments.experiments</c> table with a
///   <c>type</c> discriminator column carrying the <see cref="ExperimentType"/> name. Only
///   <see cref="ViabilidadeCelularExperiment"/> exists in this slice; further subtypes register their own
///   discriminator value and columns later.</item>
///   <item><b>Steps.</b> Modelled as an owned collection in its own <c>experiments.experiment_steps</c> table —
///   child entities of the aggregate, loaded with it.</item>
///   <item><b>Plate / wells.</b> The plate is an owned single (no columns of its own — its dimensions are domain
///   constants); its wells are an owned collection in a separate <c>experiments.wells</c> table (decision: a
///   table, not a JSON blob — 96 wells/plate). Both hang off the viability subtype.</item>
///   <item><b>Calculation result.</b> The <see cref="FormulaSnapshot"/> value object is an owned single,
///   table-split into nullable columns on the viability row (null until calculated).</item>
/// </list>
/// The owned types name their columns explicitly in snake_case because the base context skips the snake_case
/// pass for owned entity types.
/// </remarks>
internal sealed class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    public void Configure(EntityTypeBuilder<Experiment> builder)
    {
        builder.ToTable("experiments");

        builder.HasKey(experiment => experiment.Id);
        builder.Property(experiment => experiment.Id).ValueGeneratedNever();

        builder.Property(experiment => experiment.CompanyId)
            .IsRequired();

        // TPH discriminator: the experiment type name (e.g. "ViabilidadeCelular") in the "type" column. Both
        // plate assays share this single table; the plate/snapshot columns are mapped on the PlateExperiment base
        // (see ConfigurePlateExperiment) so every plate subtype inherits them identically.
        builder.HasDiscriminator(experiment => experiment.Type)
            .HasValue<ViabilidadeCelularExperiment>(ExperimentType.ViabilidadeCelular)
            .HasValue<NitricOxideExperiment>(ExperimentType.NitricOxide)
            .HasValue<VonFreiExperiment>(ExperimentType.VonFrei)
            .HasValue<TailFlickExperiment>(ExperimentType.TailFlick)
            .HasValue<RotaRodExperiment>(ExperimentType.RotaRod)
            .HasValue<HemogramaExperiment>(ExperimentType.Hemograma);

        builder.Property(experiment => experiment.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(experiment => experiment.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(experiment => experiment.Description)
            .HasMaxLength(2000);

        builder.Property(experiment => experiment.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(experiment => experiment.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(experiment => experiment.CreatedAtUtc)
            .IsRequired();

        // Steps — an owned collection in its own table. Backing field is the private list on the aggregate.
        builder.OwnsMany(experiment => experiment.Steps, ConfigureSteps);
        builder.Navigation(experiment => experiment.Steps).AutoInclude();

        // Tenant isolation: index by (company_id, id).
        builder.HasIndex(experiment => new { experiment.CompanyId, experiment.Id })
            .HasDatabaseName("ix_experiments_company_id_id");
    }

    private static void ConfigureSteps(OwnedNavigationBuilder<Experiment, ExperimentStep> steps)
    {
        steps.ToTable("experiment_steps");

        steps.WithOwner().HasForeignKey("experiment_id");
        steps.HasKey(step => step.Id);
        steps.Property(step => step.Id).HasColumnName("id").ValueGeneratedNever();
        steps.Property<Guid>("experiment_id");

        steps.Property(step => step.Order).HasColumnName("step_order").IsRequired();
        steps.Property(step => step.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        steps.Property(step => step.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        steps.Property(step => step.PerformedBy).HasColumnName("performed_by").HasMaxLength(200);
        steps.Property(step => step.PerformedAtUtc).HasColumnName("performed_at_utc");
        steps.Property(step => step.Notes).HasColumnName("notes").HasMaxLength(2000);

        steps.HasIndex("experiment_id").HasDatabaseName("ix_experiment_steps_experiment_id");
    }
}

/// <summary>
/// TPH-base configuration for every plate experiment: its owned plate/wells and the calculation-result
/// snapshot, shared by <see cref="ViabilidadeCelularExperiment"/> and <see cref="NitricOxideExperiment"/>.
/// Mapping these on the <see cref="PlateExperiment"/> base means both subtypes get identical plate columns in
/// the single TPH table — a new plate assay needs no persistence change. Applied after
/// <see cref="ExperimentConfiguration"/> so the base hierarchy is already mapped.
/// </summary>
internal sealed class PlateExperimentConfiguration
    : IEntityTypeConfiguration<PlateExperiment>
{
    public void Configure(EntityTypeBuilder<PlateExperiment> builder)
    {
        builder.Property(experiment => experiment.CompoundPartnerId)
            .HasColumnName("compound_partner_id");

        // Plate: an owned single with no scalar columns (its dimensions are domain constants). Its wells are an
        // owned collection in the "wells" table. Table-split into the experiments row via a shared key.
        builder.OwnsOne(experiment => experiment.Plate, ConfigurePlate);
        builder.Navigation(experiment => experiment.Plate).IsRequired().AutoInclude();

        // Calculation result: an owned single, table-split into nullable snapshot columns on the experiment row.
        builder.OwnsOne(experiment => experiment.CalculationResult, ConfigureSnapshot);
    }

    private static void ConfigurePlate(OwnedNavigationBuilder<PlateExperiment, Plate> plate)
    {
        // The plate itself has no columns; only its wells are persisted, in their own table.
        plate.OwnsMany(p => p.Wells, ConfigureWells);
        plate.Navigation(p => p.Wells).AutoInclude();
    }

    private static void ConfigureWells(OwnedNavigationBuilder<Plate, Well> wells)
    {
        wells.ToTable("wells");

        wells.WithOwner().HasForeignKey("experiment_id");
        wells.HasKey(well => well.Id);
        wells.Property(well => well.Id).HasColumnName("id").ValueGeneratedNever();
        wells.Property<Guid>("experiment_id");

        wells.Property(well => well.Row).HasColumnName("well_row").IsRequired();
        wells.Property(well => well.Column).HasColumnName("well_column").IsRequired();
        wells.Property(well => well.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        wells.Property(well => well.ConcentrationUm)
            .HasColumnName("concentration_um")
            .HasColumnType("numeric(18,4)");
        wells.Property(well => well.SampleId).HasColumnName("sample_id").HasMaxLength(120);
        wells.Property(well => well.RawAbsorbance)
            .HasColumnName("raw_absorbance")
            .HasColumnType("numeric(12,4)");

        wells.HasIndex("experiment_id").HasDatabaseName("ix_wells_experiment_id");
    }

    private static void ConfigureSnapshot(
        OwnedNavigationBuilder<PlateExperiment, FormulaSnapshot> snapshot)
    {
        snapshot.Property(s => s.FormulaName).HasColumnName("formula_name").HasMaxLength(100);
        snapshot.Property(s => s.FormulaExpression).HasColumnName("formula_expression").HasMaxLength(1000);
        snapshot.Property(s => s.AppliedAtUtc).HasColumnName("formula_applied_at_utc");
        snapshot.Property(s => s.ResultJson).HasColumnName("formula_result_json").HasColumnType("jsonb");
    }
}

/// <summary>
/// TPH-base configuration for every in vivo behavioural experiment (card [E11] #88): the by-value
/// <c>ProjectId</c>/<c>BatchId</c> it runs on, its owned raw <c>behavioral_measurements</c> collection, and the
/// calculation-result snapshot. The snapshot reuses the same <c>formula_*</c> columns as the plate assays — in a
/// single TPH table these are shared across the hierarchy, so a calculated von Frey and a calculated viability
/// experiment both land in the same nullable columns. Applied after <see cref="ExperimentConfiguration"/> so the
/// base hierarchy is already mapped.
/// </summary>
internal sealed class BehavioralExperimentConfiguration
    : IEntityTypeConfiguration<BehavioralExperiment>
{
    public void Configure(EntityTypeBuilder<BehavioralExperiment> builder)
    {
        builder.Property(experiment => experiment.ProjectId).HasColumnName("project_id");
        builder.Property(experiment => experiment.BatchId).HasColumnName("batch_id");

        builder.HasIndex(experiment => experiment.ProjectId)
            .HasDatabaseName("ix_experiments_project_id");

        builder.OwnsMany(experiment => experiment.Measurements, ConfigureMeasurements);
        builder.Navigation(experiment => experiment.Measurements).AutoInclude();

        // Calculation result: an owned single, table-split into the same nullable snapshot columns the plate
        // assays use (shared across the TPH hierarchy).
        builder.OwnsOne(experiment => experiment.CalculationResult, snapshot =>
        {
            snapshot.Property(s => s.FormulaName).HasColumnName("formula_name").HasMaxLength(100);
            snapshot.Property(s => s.FormulaExpression).HasColumnName("formula_expression").HasMaxLength(1000);
            snapshot.Property(s => s.AppliedAtUtc).HasColumnName("formula_applied_at_utc");
            snapshot.Property(s => s.ResultJson).HasColumnName("formula_result_json").HasColumnType("jsonb");
        });
    }

    private static void ConfigureMeasurements(
        OwnedNavigationBuilder<BehavioralExperiment, BehavioralMeasurement> measurements)
    {
        measurements.ToTable("behavioral_measurements");

        measurements.WithOwner().HasForeignKey("experiment_id");
        measurements.HasKey(measurement => measurement.Id);
        measurements.Property(measurement => measurement.Id).HasColumnName("id").ValueGeneratedNever();
        measurements.Property<Guid>("experiment_id");

        measurements.Property(measurement => measurement.AnimalId).HasColumnName("animal_id").IsRequired();
        measurements.Property(measurement => measurement.TimepointLabel)
            .HasColumnName("timepoint_label").HasMaxLength(60).IsRequired();
        measurements.Property(measurement => measurement.RawValue)
            .HasColumnName("raw_value").HasMaxLength(500).IsRequired();
        measurements.Property(measurement => measurement.RecordedBy)
            .HasColumnName("recorded_by").HasMaxLength(200).IsRequired();
        measurements.Property(measurement => measurement.RecordedAtUtc)
            .HasColumnName("recorded_at_utc").IsRequired();

        measurements.HasIndex("experiment_id").HasDatabaseName("ix_behavioral_measurements_experiment_id");
    }
}
