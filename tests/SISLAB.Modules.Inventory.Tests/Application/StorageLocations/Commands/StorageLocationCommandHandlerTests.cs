using SISLAB.Modules.Inventory.Application.StorageLocations.Commands;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StorageLocations.Commands;

/// <summary>
/// Covers the storage-location write side (card [E7] #112): register, the conservative metadata update (which
/// deliberately cannot change the type) and the active/inactive toggle that preserves the movement history.
/// Each handler delegates to the aggregate's behaviour methods and hands it back to the repository for
/// persistence; the tenant/company stamping is the write-side global query filter's job, out of scope here.
/// </summary>
public sealed class StorageLocationCommandHandlerTests
{
    // --- Register ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Register_creates_an_active_location_and_returns_its_id()
    {
        var locations = new FakeStorageLocationRepository();
        var handler = new RegisterStorageLocationCommandHandler(locations);

        Guid id = await handler.HandleAsync(new RegisterStorageLocationCommand(
            Name: "Almoxarifado",
            Type: StorageLocationType.GeneralStorage,
            Description: "Prateleira central",
            TemperatureMinCelsius: null,
            TemperatureMaxCelsius: null));

        StorageLocation persisted = (await locations.FindByIdAsync(id))!;
        Assert.Equal("Almoxarifado", persisted.Name);
        Assert.Equal(StorageLocationType.GeneralStorage, persisted.Type);
        Assert.Equal("Prateleira central", persisted.Description);
        Assert.True(persisted.IsActive);
        Assert.Null(persisted.TemperatureRange);
    }

    [Fact]
    public async Task Register_sets_the_temperature_range_for_a_refrigerated_location()
    {
        var locations = new FakeStorageLocationRepository();
        var handler = new RegisterStorageLocationCommandHandler(locations);

        Guid id = await handler.HandleAsync(new RegisterStorageLocationCommand(
            Name: "Freezer -80",
            Type: StorageLocationType.Refrigerated,
            Description: null,
            TemperatureMinCelsius: -80m,
            TemperatureMaxCelsius: -20m));

        StorageLocation persisted = (await locations.FindByIdAsync(id))!;
        Assert.NotNull(persisted.TemperatureRange);
        Assert.Equal(-80m, persisted.TemperatureRange!.MinimumCelsius);
        Assert.Equal(-20m, persisted.TemperatureRange.MaximumCelsius);
    }

    [Fact]
    public async Task Register_rejects_a_temperature_range_on_a_non_refrigerated_location()
    {
        var handler = new RegisterStorageLocationCommandHandler(new FakeStorageLocationRepository());

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new RegisterStorageLocationCommand(
            Name: "Bancada",
            Type: StorageLocationType.GeneralStorage,
            Description: null,
            TemperatureMinCelsius: 2m,
            TemperatureMaxCelsius: 8m)));
    }

    // --- Update --------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_renames_and_redescribes_the_location_without_touching_the_type()
    {
        StorageLocation location = StorageLocation.Register(
            "Armário", StorageLocationType.ReagentCabinet, "Descrição antiga");
        var locations = new FakeStorageLocationRepository().Seed(location);
        var handler = new UpdateStorageLocationCommandHandler(locations);

        await handler.HandleAsync(new UpdateStorageLocationCommand(
            StorageLocationId: location.Id,
            Name: "Armário de reagentes",
            Description: "Descrição nova",
            TemperatureMinCelsius: null,
            TemperatureMaxCelsius: null));

        StorageLocation persisted = (await locations.FindByIdAsync(location.Id))!;
        Assert.Equal("Armário de reagentes", persisted.Name);
        Assert.Equal("Descrição nova", persisted.Description);
        Assert.Equal(StorageLocationType.ReagentCabinet, persisted.Type); // unchanged
    }

    [Fact]
    public async Task Update_can_redefine_the_temperature_range_of_a_refrigerated_location()
    {
        StorageLocation location = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        var locations = new FakeStorageLocationRepository().Seed(location);
        var handler = new UpdateStorageLocationCommandHandler(locations);

        await handler.HandleAsync(new UpdateStorageLocationCommand(
            location.Id, "Geladeira 1", null, TemperatureMinCelsius: 2m, TemperatureMaxCelsius: 8m));

        StorageLocation persisted = (await locations.FindByIdAsync(location.Id))!;
        Assert.NotNull(persisted.TemperatureRange);
        Assert.Equal(2m, persisted.TemperatureRange!.MinimumCelsius);
        Assert.Equal(8m, persisted.TemperatureRange.MaximumCelsius);
    }

    [Fact]
    public async Task Update_rejects_a_temperature_range_on_a_non_refrigerated_location()
    {
        StorageLocation location = StorageLocation.Register("Bancada", StorageLocationType.GeneralStorage);
        var handler = new UpdateStorageLocationCommandHandler(
            new FakeStorageLocationRepository().Seed(location));

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new UpdateStorageLocationCommand(
            location.Id, "Bancada 1", null, TemperatureMinCelsius: 2m, TemperatureMaxCelsius: 8m)));
    }

    [Fact]
    public async Task Update_fails_when_the_location_does_not_exist()
    {
        var handler = new UpdateStorageLocationCommandHandler(new FakeStorageLocationRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new UpdateStorageLocationCommand(
            Guid.NewGuid(), "X", null, null, null)));
    }

    // --- Toggle status -------------------------------------------------------------------------------------

    [Fact]
    public async Task Toggle_deactivates_an_active_location()
    {
        StorageLocation location = StorageLocation.Register("Bancada", StorageLocationType.GeneralStorage);
        var locations = new FakeStorageLocationRepository().Seed(location);
        var handler = new ToggleStorageLocationStatusCommandHandler(locations);

        await handler.HandleAsync(new ToggleStorageLocationStatusCommand(location.Id, IsActive: false));

        StorageLocation persisted = (await locations.FindByIdAsync(location.Id))!;
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task Toggle_reactivates_an_inactive_location()
    {
        StorageLocation location = StorageLocation.Register("Bancada", StorageLocationType.GeneralStorage);
        location.Deactivate();
        var locations = new FakeStorageLocationRepository().Seed(location);
        var handler = new ToggleStorageLocationStatusCommandHandler(locations);

        await handler.HandleAsync(new ToggleStorageLocationStatusCommand(location.Id, IsActive: true));

        StorageLocation persisted = (await locations.FindByIdAsync(location.Id))!;
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task Toggle_fails_when_the_location_does_not_exist()
    {
        var handler = new ToggleStorageLocationStatusCommandHandler(new FakeStorageLocationRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new ToggleStorageLocationStatusCommand(Guid.NewGuid(), IsActive: false)));
    }
}
