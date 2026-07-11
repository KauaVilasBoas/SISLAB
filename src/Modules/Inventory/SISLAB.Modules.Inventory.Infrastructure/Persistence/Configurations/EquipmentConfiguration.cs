using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Inventory.Domain.Equipments;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Equipment"/> aggregate.
/// </summary>
/// <remarks>
/// Value-object mapping decisions (card [E3] #27):
/// <list type="bullet">
///   <item><see cref="CalibrationSchedule"/> → owned type on the owner's table with nullable
///   <c>last_calibration</c> / <c>next_calibration</c> columns; a null <c>last_calibration</c> means
///   calibration is not applicable (n/a).</item>
///   <item><see cref="MaintenanceRecord"/> collection → owned into a child table
///   <c>equipment_maintenances</c> (a real one-to-many owned by the aggregate). A maintenance has no
///   natural business key (two records may share the same date and type), so a surrogate shadow key
///   pairs with the owner id.</item>
/// </list>
/// The <c>StorageLocationId</c> is a plain nullable value (a location referenced by value), never a
/// cross-aggregate foreign key.
/// </remarks>
internal sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipment");

        builder.HasKey(equipment => equipment.Id);
        builder.Property(equipment => equipment.Id).ValueGeneratedNever();

        builder.Property(equipment => equipment.CompanyId)
            .IsRequired();

        builder.Property(equipment => equipment.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(equipment => equipment.AssetTag)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(equipment => equipment.Brand)
            .HasMaxLength(120);

        builder.Property(equipment => equipment.Model)
            .HasMaxLength(120);

        // Storage location referenced by value — nullable, no cross-aggregate FK/navigation.
        builder.Property(equipment => equipment.StorageLocationId);

        builder.Property(equipment => equipment.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Calibration schedule — a whole-owned nullable value object. Both columns are null when
        // calibration does not apply (n/a). Column names are set explicitly because the base skips the
        // snake_case pass for owned types.
        builder.OwnsOne(equipment => equipment.Calibration, calibration =>
        {
            calibration.Property(schedule => schedule.LastCalibration)
                .HasColumnName("last_calibration");

            calibration.Property(schedule => schedule.NextCalibration)
                .HasColumnName("next_calibration");
        });

        // Maintenance history — owned one-to-many child table with a surrogate shadow key (a record
        // has no natural unique key). The aggregate mutates the collection through its methods; EF
        // persists it via the private backing field.
        builder.OwnsMany(equipment => equipment.MaintenanceRecords, maintenance =>
        {
            maintenance.ToTable("equipment_maintenances");

            maintenance.WithOwner().HasForeignKey("equipment_id");

            maintenance.Property<long>("id");
            maintenance.HasKey("id");

            maintenance.Property(record => record.Date)
                .HasColumnName("date")
                .IsRequired();

            maintenance.Property(record => record.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            maintenance.Property(record => record.Notes)
                .HasColumnName("notes")
                .HasMaxLength(1000);
        });

        // The maintenance collection is populated through the aggregate's methods and read via the field.
        builder.Navigation(equipment => equipment.MaintenanceRecords)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Tenant isolation: index by (company_id, id). company_id NOT NULL is enforced above.
        builder.HasIndex(equipment => new { equipment.CompanyId, equipment.Id })
            .HasDatabaseName("ix_equipment_company_id_id");
    }
}
