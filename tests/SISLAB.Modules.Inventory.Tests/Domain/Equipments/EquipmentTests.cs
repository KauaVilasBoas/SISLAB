using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.Modules.Inventory.Domain.Equipments.Events;
using SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Tests.Domain.Equipments;

public sealed class EquipmentTests
{
    private static Equipment NewEquipment() =>
        Equipment.Register("Leitora de placas", "PAT-0041");

    [Fact]
    public void Register_captures_the_attributes_normalizes_them_and_starts_available()
    {
        Equipment equipment = Equipment.Register(
            name: "  Leitora de placas  ",
            assetTag: "  PAT-0041  ",
            brand: "  BioTek  ",
            model: "  Synergy H1  ");

        Assert.Equal("Leitora de placas", equipment.Name);
        Assert.Equal("PAT-0041", equipment.AssetTag);
        Assert.Equal("BioTek", equipment.Brand);
        Assert.Equal("Synergy H1", equipment.Model);
        Assert.Equal(EquipmentStatus.Available, equipment.Status);
        Assert.Null(equipment.Calibration);
        Assert.Null(equipment.StorageLocationId);
        Assert.Empty(equipment.MaintenanceRecords);
    }

    [Fact]
    public void Register_allows_an_equipment_without_optional_data()
    {
        Equipment equipment = NewEquipment();

        Assert.Null(equipment.Brand);
        Assert.Null(equipment.Model);
        Assert.Null(equipment.StorageLocationId);
    }

    [Fact]
    public void Register_keeps_a_supplied_storage_location()
    {
        Guid location = Guid.NewGuid();

        Equipment equipment = Equipment.Register(
            "Freezer -80", "PAT-0002", storageLocationId: location);

        Assert.Equal(location, equipment.StorageLocationId);
    }

    [Fact]
    public void Register_treats_an_empty_storage_location_guid_as_none()
    {
        Equipment equipment = Equipment.Register(
            "Freezer -80", "PAT-0002", storageLocationId: Guid.Empty);

        Assert.Null(equipment.StorageLocationId);
    }

    [Fact]
    public void Register_raises_EquipmentRegistered()
    {
        Equipment equipment = NewEquipment();

        EquipmentRegisteredEvent registered =
            Assert.IsType<EquipmentRegisteredEvent>(Assert.Single(equipment.DomainEvents));
        Assert.Equal(equipment.Id, registered.EquipmentId);
        Assert.Equal("PAT-0041", registered.AssetTag);
    }

    [Fact]
    public void Register_rejects_a_blank_name()
        => Assert.Throws<DomainException>(() => Equipment.Register("  ", "PAT-1"));

    [Fact]
    public void Register_rejects_a_blank_asset_tag()
        => Assert.Throws<DomainException>(() => Equipment.Register("Vórtex", "  "));

    [Fact]
    public void Equipment_is_tenant_scoped()
        => Assert.IsAssignableFrom<ITenantEntity>(NewEquipment());

    [Theory]
    [InlineData(EquipmentStatus.Available, EquipmentStatus.InUse, true)]
    [InlineData(EquipmentStatus.Available, EquipmentStatus.UnderMaintenance, true)]
    [InlineData(EquipmentStatus.Available, EquipmentStatus.Inactive, true)]
    [InlineData(EquipmentStatus.InUse, EquipmentStatus.Available, true)]
    [InlineData(EquipmentStatus.InUse, EquipmentStatus.UnderMaintenance, true)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Available, true)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Inactive, true)]
    [InlineData(EquipmentStatus.Inactive, EquipmentStatus.Available, true)]
    // Rejected moves.
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.InUse, false)]
    [InlineData(EquipmentStatus.Inactive, EquipmentStatus.InUse, false)]
    [InlineData(EquipmentStatus.Inactive, EquipmentStatus.UnderMaintenance, false)]
    public void CanChangeStatusTo_reflects_the_transition_policy(
        EquipmentStatus from, EquipmentStatus to, bool expected)
    {
        Equipment equipment = Equipment.Register("Autoclave", "PAT-9", status: from);

        Assert.Equal(expected, equipment.CanChangeStatusTo(to));
    }

    [Fact]
    public void ChangeStatus_applies_a_valid_transition_and_raises_an_event()
    {
        Equipment equipment = NewEquipment();
        equipment.ClearDomainEvents();

        equipment.ChangeStatus(EquipmentStatus.InUse);

        Assert.Equal(EquipmentStatus.InUse, equipment.Status);
        EquipmentStatusChangedEvent changed =
            Assert.IsType<EquipmentStatusChangedEvent>(Assert.Single(equipment.DomainEvents));
        Assert.Equal(EquipmentStatus.Available, changed.PreviousStatus);
        Assert.Equal(EquipmentStatus.InUse, changed.NewStatus);
    }

    [Fact]
    public void ChangeStatus_to_the_same_status_is_a_no_op()
    {
        Equipment equipment = NewEquipment();
        equipment.ClearDomainEvents();

        equipment.ChangeStatus(EquipmentStatus.Available);

        Assert.Equal(EquipmentStatus.Available, equipment.Status);
        Assert.Empty(equipment.DomainEvents);
    }

    [Fact]
    public void ChangeStatus_rejects_an_invalid_transition()
    {
        Equipment equipment = Equipment.Register(
            "Autoclave", "PAT-9", status: EquipmentStatus.UnderMaintenance);

        Assert.Throws<DomainException>(() => equipment.ChangeStatus(EquipmentStatus.InUse));
    }

    [Fact]
    public void CanChangeStatusTo_the_current_status_is_false()
        => Assert.False(NewEquipment().CanChangeStatusTo(EquipmentStatus.Available));

    [Fact]
    public void DescribeModel_updates_brand_and_model_and_blank_clears_them()
    {
        Equipment equipment = Equipment.Register("X", "PAT-1", brand: "Old", model: "M-1");

        equipment.DescribeModel("  BioTek  ", "  ");

        Assert.Equal("BioTek", equipment.Brand);
        Assert.Null(equipment.Model);
    }

    [Fact]
    public void RelocateTo_updates_and_clears_the_location()
    {
        Equipment equipment = NewEquipment();
        Guid location = Guid.NewGuid();

        equipment.RelocateTo(location);
        Assert.Equal(location, equipment.StorageLocationId);

        equipment.RelocateTo(null);
        Assert.Null(equipment.StorageLocationId);
    }

    [Fact]
    public void RecordMaintenance_appends_to_the_history_and_raises_an_event()
    {
        Equipment equipment = NewEquipment();
        equipment.ClearDomainEvents();

        equipment.RecordMaintenance(
            MaintenanceRecord.Create(new DateOnly(2026, 3, 10), MaintenanceType.Preventive, "Troca de lâmpada"));

        MaintenanceRecord record = Assert.Single(equipment.MaintenanceRecords);
        Assert.Equal(MaintenanceType.Preventive, record.Type);
        Assert.Equal("Troca de lâmpada", record.Notes);
        EquipmentMaintenanceRecordedEvent recorded =
            Assert.IsType<EquipmentMaintenanceRecordedEvent>(Assert.Single(equipment.DomainEvents));
        Assert.Equal(equipment.Id, recorded.EquipmentId);
        Assert.Equal(MaintenanceType.Preventive, recorded.Type);
    }

    [Fact]
    public void RecordMaintenance_allows_two_records_with_the_same_date_and_type()
    {
        Equipment equipment = NewEquipment();
        var date = new DateOnly(2026, 3, 10);

        equipment.RecordMaintenance(MaintenanceRecord.Create(date, MaintenanceType.Corrective));
        equipment.RecordMaintenance(MaintenanceRecord.Create(date, MaintenanceType.Corrective));

        Assert.Equal(2, equipment.MaintenanceRecords.Count);
    }

    [Fact]
    public void DefineCalibration_sets_and_clears_the_schedule()
    {
        Equipment equipment = NewEquipment();
        CalibrationSchedule schedule =
            CalibrationSchedule.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1));

        equipment.DefineCalibration(schedule);
        Assert.Equal(schedule, equipment.Calibration);

        equipment.DefineCalibration(null);
        Assert.Null(equipment.Calibration);
    }

    [Fact]
    public void IsCalibrationOverdue_is_false_when_calibration_is_not_applicable()
    {
        Equipment vortex = Equipment.Register("Vórtex", "PAT-7");

        Assert.False(vortex.IsCalibrationOverdue(FixedClock.On(2026, 7, 11)));
    }

    [Fact]
    public void IsCalibrationOverdue_is_true_when_the_due_date_has_passed()
    {
        Equipment equipment = Equipment.Register(
            "Espectrofotômetro", "PAT-3",
            calibration: CalibrationSchedule.Create(new DateOnly(2025, 6, 1), new DateOnly(2026, 6, 1)));

        Assert.True(equipment.IsCalibrationOverdue(FixedClock.On(2026, 7, 11)));
    }

    [Fact]
    public void IsCalibrationOverdue_is_false_before_the_due_date()
    {
        Equipment equipment = Equipment.Register(
            "Espectrofotômetro", "PAT-3",
            calibration: CalibrationSchedule.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 1)));

        Assert.False(equipment.IsCalibrationOverdue(FixedClock.On(2026, 7, 11)));
    }
}
