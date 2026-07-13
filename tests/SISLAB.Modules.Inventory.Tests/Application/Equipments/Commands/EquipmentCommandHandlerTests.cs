using SISLAB.Modules.Inventory.Application.Equipments.Commands;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.Equipments.Commands;

public sealed class EquipmentCommandHandlerTests
{
    [Fact]
    public async Task Register_creates_and_persists_the_equipment()
    {
        var equipments = new FakeEquipmentRepository();
        var handler = new RegisterEquipmentCommandHandler(equipments);

        Guid id = await handler.HandleAsync(new RegisterEquipmentCommand(
            "Leitora de placas", "PAT-0041", Brand: "BioTek", Model: "Synergy H1",
            StorageLocationId: null, Status: EquipmentStatus.Available,
            LastCalibration: null, NextCalibration: null));

        Equipment created = Assert.IsType<Equipment>(equipments.LastAdded);
        Assert.Equal(id, created.Id);
        Assert.Equal("Leitora de placas", created.Name);
        Assert.Equal("PAT-0041", created.AssetTag);
        Assert.Null(created.Calibration);
    }

    [Fact]
    public async Task Register_builds_the_calibration_schedule_when_a_last_date_is_given()
    {
        var equipments = new FakeEquipmentRepository();
        var handler = new RegisterEquipmentCommandHandler(equipments);

        await handler.HandleAsync(new RegisterEquipmentCommand(
            "Espectrofotômetro", "PAT-3", Brand: null, Model: null,
            StorageLocationId: null, Status: EquipmentStatus.Available,
            LastCalibration: new DateOnly(2026, 1, 1), NextCalibration: new DateOnly(2026, 12, 1)));

        Equipment created = Assert.IsType<Equipment>(equipments.LastAdded);
        Assert.NotNull(created.Calibration);
        Assert.Equal(new DateOnly(2026, 1, 1), created.Calibration!.LastCalibration);
        Assert.Equal(new DateOnly(2026, 12, 1), created.Calibration.NextCalibration);
    }

    [Fact]
    public async Task Update_changes_the_equipment_and_persists_it()
    {
        Equipment equipment = Equipment.Register("Old", "PAT-0", brand: "B", model: "M");
        var equipments = new FakeEquipmentRepository().Seed(equipment);
        var handler = new UpdateEquipmentCommandHandler(equipments);

        await handler.HandleAsync(new UpdateEquipmentCommand(
            equipment.Id, "Centrífuga", "PAT-5", Brand: "Eppendorf", Model: "5804 R",
            StorageLocationId: null));

        Assert.Equal("Centrífuga", equipment.Name);
        Assert.Equal("PAT-5", equipment.AssetTag);
        Assert.Equal("Eppendorf", equipment.Brand);
        Assert.Same(equipment, equipments.LastUpdated);
    }

    [Fact]
    public async Task Update_fails_when_the_equipment_does_not_exist()
    {
        var handler = new UpdateEquipmentCommandHandler(new FakeEquipmentRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new UpdateEquipmentCommand(Guid.NewGuid(), "X", "PAT-1", null, null, null)));
    }

    [Fact]
    public async Task ChangeStatus_applies_a_valid_transition()
    {
        Equipment equipment = Equipment.Register("Autoclave", "PAT-9");
        var equipments = new FakeEquipmentRepository().Seed(equipment);
        var handler = new ChangeEquipmentStatusCommandHandler(equipments);

        await handler.HandleAsync(new ChangeEquipmentStatusCommand(equipment.Id, EquipmentStatus.InUse));

        Assert.Equal(EquipmentStatus.InUse, equipment.Status);
        Assert.Same(equipment, equipments.LastUpdated);
    }

    [Fact]
    public async Task ChangeStatus_propagates_an_invalid_transition_as_a_domain_error()
    {
        Equipment equipment = Equipment.Register(
            "Autoclave", "PAT-9", status: EquipmentStatus.Inactive);
        var equipments = new FakeEquipmentRepository().Seed(equipment);
        var handler = new ChangeEquipmentStatusCommandHandler(equipments);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new ChangeEquipmentStatusCommand(equipment.Id, EquipmentStatus.InUse)));
    }

    [Fact]
    public async Task DefineCalibration_sets_the_schedule_and_persists_it()
    {
        Equipment equipment = Equipment.Register("Espectrofotômetro", "PAT-3");
        var equipments = new FakeEquipmentRepository().Seed(equipment);
        var handler = new DefineEquipmentCalibrationCommandHandler(equipments);

        await handler.HandleAsync(new DefineEquipmentCalibrationCommand(
            equipment.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1)));

        Assert.NotNull(equipment.Calibration);
        Assert.Equal(new DateOnly(2026, 12, 1), equipment.Calibration!.NextCalibration);
        Assert.Same(equipment, equipments.LastUpdated);
    }

    [Fact]
    public async Task DefineCalibration_with_a_null_last_date_clears_the_schedule()
    {
        Equipment equipment = Equipment.Register(
            "Vórtex", "PAT-7",
            calibration: CalibrationSchedule.Create(new DateOnly(2026, 1, 1)));
        var equipments = new FakeEquipmentRepository().Seed(equipment);
        var handler = new DefineEquipmentCalibrationCommandHandler(equipments);

        await handler.HandleAsync(new DefineEquipmentCalibrationCommand(equipment.Id, null, null));

        Assert.Null(equipment.Calibration);
    }

    [Fact]
    public async Task RecordMaintenance_appends_a_record_and_persists_it()
    {
        Equipment equipment = Equipment.Register("Centrífuga", "PAT-5");
        var equipments = new FakeEquipmentRepository().Seed(equipment);
        var handler = new RecordEquipmentMaintenanceCommandHandler(equipments);

        await handler.HandleAsync(new RecordEquipmentMaintenanceCommand(
            equipment.Id, new DateOnly(2026, 3, 10), MaintenanceType.Preventive, "Revisão anual"));

        MaintenanceRecord record = Assert.Single(equipment.MaintenanceRecords);
        Assert.Equal(MaintenanceType.Preventive, record.Type);
        Assert.Equal("Revisão anual", record.Notes);
        Assert.Same(equipment, equipments.LastUpdated);
    }

    [Fact]
    public async Task RecordMaintenance_fails_when_the_equipment_does_not_exist()
    {
        var handler = new RecordEquipmentMaintenanceCommandHandler(new FakeEquipmentRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RecordEquipmentMaintenanceCommand(
                Guid.NewGuid(), new DateOnly(2026, 3, 10), MaintenanceType.Corrective, null)));
    }
}
