using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.ExperimentalModels;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ExperimentalModel"/> aggregate (schema <c>configuration</c>, SISLAB-04).
/// </summary>
/// <remarks>
/// <para>
/// <b>Scalar value objects → columns.</b> <see cref="InductionProtocol"/> and <see cref="DilutionDefaults"/> are
/// owned singles table-split onto the model row (a few numeric/text columns), mirroring how <c>RangeBounds</c> is
/// mapped — reconstituted through their validated factories on read-back so an invalid persisted protocol fails
/// fast.
/// </para>
/// <para>
/// <b>Collection value objects → jsonb.</b> <see cref="StandardTimepoints"/>, <see cref="ApplicableParameters"/> and
/// <see cref="StandardGroups"/> are single conceptual values whose order/shape matters, so each is serialized to a
/// <c>jsonb</c> column via a value converter over a plain DTO, and rebuilt through the aggregate's validated
/// factories. A child table would add no query value here (these are always loaded with the model), and JSON keeps
/// the aggregate a single row — the same "flatten the VO onto the row" decision taken for category aliases.
/// </para>
/// The model name is unique per tenant.
/// </remarks>
internal sealed class ExperimentalModelConfiguration : IEntityTypeConfiguration<ExperimentalModel>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    public void Configure(EntityTypeBuilder<ExperimentalModel> builder)
    {
        builder.ToTable("experimental_models");

        builder.HasKey(model => model.Id);
        builder.Property(model => model.Id).ValueGeneratedNever();

        builder.Property(model => model.CompanyId).IsRequired();

        builder.Property(model => model.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(model => model.Description)
            .HasMaxLength(500);

        // Induction protocol: an owned single table-split into three integer columns, rebuilt through the
        // validated InductionProtocol.Of factory on read-back.
        builder.OwnsOne(model => model.Induction, induction =>
        {
            induction.Property(protocol => protocol.Administrations)
                .HasColumnName("induction_administrations").IsRequired();
            induction.Property(protocol => protocol.IntervalDays)
                .HasColumnName("induction_interval_days").IsRequired();
            induction.Property(protocol => protocol.ReferenceDayAfterInduction)
                .HasColumnName("induction_reference_day").IsRequired();
        });
        builder.Navigation(model => model.Induction).IsRequired();

        // Dilution defaults: an owned single carrying the g:µL relation (a nested VO flattened to one numeric
        // column) and the default diluent name.
        builder.OwnsOne(model => model.DilutionDefaults, dilution =>
        {
            dilution.Property(defaults => defaults.DefaultDiluent)
                .HasColumnName("default_diluent").HasMaxLength(120).IsRequired();

            dilution.OwnsOne(defaults => defaults.Ratio, ratio =>
                ratio.Property(r => r.MicrolitresPerGram)
                    .HasColumnName("ratio_microlitres_per_gram")
                    .HasColumnType("numeric(18,4)")
                    .IsRequired());
            dilution.Navigation(defaults => defaults.Ratio).IsRequired();
        });
        builder.Navigation(model => model.DilutionDefaults).IsRequired();

        // Timepoints value object → jsonb: an ordered list of labels, rebuilt through StandardTimepoints.From.
        builder.Property(model => model.Timepoints)
            .HasColumnName("timepoints")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                timepoints => JsonSerializer.Serialize(timepoints.Labels, JsonOptions),
                value => StandardTimepoints.From(
                    JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>()));

        // Applicable parameters value object → jsonb: a set of parameter codes, rebuilt through
        // ApplicableParameters.From.
        builder.Property(model => model.Parameters)
            .HasColumnName("parameters")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                parameters => JsonSerializer.Serialize(parameters.Codes, JsonOptions),
                value => ApplicableParameters.From(
                    JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>()));

        // Standard groups value object → jsonb: the ordered default group design, rebuilt through StandardGroups
        // (and each StandardGroup) validated factory from a flat persistence DTO.
        builder.Property(model => model.Groups)
            .HasColumnName("groups")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                groups => JsonSerializer.Serialize(StandardGroupRecord.From(groups), JsonOptions),
                value => StandardGroupRecord.To(
                    JsonSerializer.Deserialize<List<StandardGroupRecord>>(value, JsonOptions)
                    ?? new List<StandardGroupRecord>()));

        // Model name is unique per tenant.
        builder.HasIndex(model => new { model.CompanyId, model.Name })
            .IsUnique()
            .HasDatabaseName("ux_experimental_models_company_id_name");

        // Tenant isolation access path.
        builder.HasIndex(model => new { model.CompanyId, model.Id })
            .HasDatabaseName("ix_experimental_models_company_id_id");
    }

    /// <summary>
    /// Flat persistence shape of a <see cref="StandardGroup"/> for the <c>groups</c> jsonb column. Keeping a DTO
    /// (rather than serializing the value object directly) decouples the stored JSON from the domain type's private
    /// members and routes read-back through the validated <see cref="StandardGroup"/> factories.
    /// </summary>
    private sealed record StandardGroupRecord(string Name, StandardGroupKind Kind, decimal? DoseAmount, string? DoseUnit)
    {
        public static List<StandardGroupRecord> From(StandardGroups groups)
            => groups.Groups
                .Select(group => new StandardGroupRecord(group.Name, group.Kind, group.DoseAmount, group.DoseUnit))
                .ToList();

        public static StandardGroups To(IEnumerable<StandardGroupRecord> records)
            => StandardGroups.From(records.Select(ToDomain));

        private static StandardGroup ToDomain(StandardGroupRecord record)
            => record.Kind == StandardGroupKind.Dose
                ? StandardGroup.Dosed(record.Name, record.DoseAmount ?? 0m, record.DoseUnit ?? string.Empty)
                : StandardGroup.NonDosed(record.Name, record.Kind);
    }
}
