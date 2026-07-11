using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Domain.ValueObjects;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="StorageLocation"/> aggregate.
/// </summary>
/// <remarks>
/// Value-object mapping (card [E3] #25): <see cref="TemperatureRange"/> → owned type with
/// <c>temp_min</c> / <c>temp_max</c> columns, both null when the location is not
/// <see cref="StorageLocationType.Refrigerated"/> (a whole-owned null reference).
/// </remarks>
internal sealed class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
{
    public void Configure(EntityTypeBuilder<StorageLocation> builder)
    {
        builder.ToTable("storage_locations");

        builder.HasKey(location => location.Id);
        builder.Property(location => location.Id).ValueGeneratedNever();

        builder.Property(location => location.CompanyId)
            .IsRequired();

        builder.Property(location => location.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(location => location.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(location => location.Description)
            .HasMaxLength(500);

        builder.Property(location => location.IsActive)
            .IsRequired();

        // Target conservation range — only ever set for a refrigerated location.
        builder.OwnsOne(location => location.TemperatureRange, range =>
        {
            range.Property(r => r.MinimumCelsius)
                .HasColumnName("temp_min")
                .HasColumnType("numeric(6,2)");

            range.Property(r => r.MaximumCelsius)
                .HasColumnName("temp_max")
                .HasColumnType("numeric(6,2)");
        });

        // Tenant isolation: index by (company_id, id). company_id NOT NULL is enforced above.
        builder.HasIndex(location => new { location.CompanyId, location.Id })
            .HasDatabaseName("ix_storage_locations_company_id_id");
    }
}
