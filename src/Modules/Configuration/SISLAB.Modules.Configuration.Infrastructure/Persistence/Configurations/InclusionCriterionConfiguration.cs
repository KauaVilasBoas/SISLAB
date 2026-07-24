using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.InclusionCriteria;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="InclusionCriterion"/> aggregate (schema <c>configuration</c>, SISLAB-02).
/// </summary>
/// <remarks>
/// <b>Threshold → operator + value columns.</b> <see cref="InclusionThreshold"/> is mapped as an owned single instance
/// onto <c>operator</c> (the enum stored by name) and <c>threshold</c>, rebuilt through the validated factory on
/// read-back. A tenant has at most one criterion per parameter, so <c>(company, parameter_code)</c> is unique.
/// </remarks>
internal sealed class InclusionCriterionConfiguration : IEntityTypeConfiguration<InclusionCriterion>
{
    public void Configure(EntityTypeBuilder<InclusionCriterion> builder)
    {
        builder.ToTable("inclusion_criteria");

        builder.HasKey(criterion => criterion.Id);
        builder.Property(criterion => criterion.Id).ValueGeneratedNever();

        builder.Property(criterion => criterion.CompanyId).IsRequired();

        builder.Property(criterion => criterion.ParameterCode)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(criterion => criterion.Unit)
            .IsRequired()
            .HasMaxLength(30);

        // Threshold value object: the operator (stored by name) + the numeric threshold kept together via an owned
        // single instance, rebuilt through the validated InclusionThreshold.Of factory on read-back.
        builder.OwnsOne(criterion => criterion.Threshold, threshold =>
        {
            threshold.Property(t => t.Operator)
                .HasColumnName("operator")
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            threshold.Property(t => t.Value)
                .HasColumnName("threshold")
                .HasColumnType("numeric(18,4)")
                .IsRequired();
        });
        builder.Navigation(criterion => criterion.Threshold).IsRequired();

        // A tenant has one criterion per parameter: the natural key is unique per company.
        builder.HasIndex(criterion => new { criterion.CompanyId, criterion.ParameterCode })
            .IsUnique()
            .HasDatabaseName("ux_inclusion_criteria_company_id_parameter_code");

        // Tenant isolation access path.
        builder.HasIndex(criterion => new { criterion.CompanyId, criterion.Id })
            .HasDatabaseName("ix_inclusion_criteria_company_id_id");
    }
}
