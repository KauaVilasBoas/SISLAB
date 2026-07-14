using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.Units;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Unit"/> aggregate (schema <c>configuration</c>). The unit symbol
/// is unique per tenant.
/// </summary>
internal sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("units");

        builder.HasKey(unit => unit.Id);
        builder.Property(unit => unit.Id).ValueGeneratedNever();

        builder.Property(unit => unit.CompanyId).IsRequired();

        builder.Property(unit => unit.Symbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(unit => unit.Name)
            .IsRequired()
            .HasMaxLength(80);

        // Unit symbol is unique per tenant (its identity within the company).
        builder.HasIndex(unit => new { unit.CompanyId, unit.Symbol })
            .IsUnique()
            .HasDatabaseName("ux_units_company_id_symbol");

        // Tenant isolation access path.
        builder.HasIndex(unit => new { unit.CompanyId, unit.Id })
            .HasDatabaseName("ix_units_company_id_id");
    }
}
