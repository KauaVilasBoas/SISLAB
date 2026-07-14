using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Domain.StorageLocations.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Tests.Domain.StorageLocations;

public sealed class StorageLocationTests
{
    private static readonly UnitOfMeasure Ml = UnitOfMeasure.Milliliter;

    private static StorageLocation NewLocation(
        StorageLocationType type = StorageLocationType.GeneralStorage) =>
        StorageLocation.Register("Almoxarifado", type);

    // The category is referenced by value (a per-tenant Configuration category, card [E12] #76); the storage
    // rule keys off the item's IsControlled flag, not the category, so a fixed id suffices here.
    private static readonly Guid Category = Guid.NewGuid();

    private static StockItem NewItem(bool controlled) =>
        StockItem.Register(
            name: controlled ? "Cetamina 10%" : "DMSO",
            categoryId: Category,
            storageLocationId: Guid.NewGuid(),
            initialQuantity: Quantity.Of(100m, Ml),
            minimumQuantity: Quantity.Of(10m, Ml),
            isControlled: controlled);

    [Fact]
    public void Register_captures_the_descriptive_attributes_and_starts_active()
    {
        StorageLocation location = StorageLocation.Register(
            name: "  Freezer -80 °C  ",
            type: StorageLocationType.Refrigerated,
            description: "  Amostras celulares  ",
            temperatureRange: TemperatureRange.Between(-80m, -20m));

        Assert.Equal("Freezer -80 °C", location.Name);
        Assert.Equal(StorageLocationType.Refrigerated, location.Type);
        Assert.Equal("Amostras celulares", location.Description);
        Assert.Equal(TemperatureRange.Between(-80m, -20m), location.TemperatureRange);
        Assert.True(location.IsActive);
    }

    [Fact]
    public void Register_raises_StorageLocationRegistered()
    {
        StorageLocation location = NewLocation(StorageLocationType.Controlled);

        StorageLocationRegisteredEvent registered =
            Assert.IsType<StorageLocationRegisteredEvent>(Assert.Single(location.DomainEvents));
        Assert.Equal(location.Id, registered.StorageLocationId);
        Assert.Equal(StorageLocationType.Controlled, registered.Type);
    }

    [Fact]
    public void Register_rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() =>
            StorageLocation.Register("   ", StorageLocationType.GeneralStorage));
    }

    [Fact]
    public void StorageLocation_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(NewLocation());
    }

    [Fact]
    public void Register_rejects_a_temperature_range_on_a_non_refrigerated_location()
    {
        Assert.Throws<DomainException>(() => StorageLocation.Register(
            name: "Almoxarifado",
            type: StorageLocationType.GeneralStorage,
            temperatureRange: TemperatureRange.Between(2m, 8m)));
    }

    [Fact]
    public void DefineTemperatureRange_is_allowed_only_on_a_refrigerated_location()
    {
        StorageLocation fridge = NewLocation(StorageLocationType.Refrigerated);

        fridge.DefineTemperatureRange(TemperatureRange.Between(2m, 8m));

        Assert.Equal(TemperatureRange.Between(2m, 8m), fridge.TemperatureRange);
    }

    [Fact]
    public void DefineTemperatureRange_is_rejected_on_a_non_refrigerated_location()
    {
        StorageLocation warehouse = NewLocation(StorageLocationType.GeneralStorage);

        Assert.Throws<DomainException>(() =>
            warehouse.DefineTemperatureRange(TemperatureRange.Between(2m, 8m)));
    }

    [Fact]
    public void DescribeAs_blank_clears_the_description()
    {
        StorageLocation location = StorageLocation.Register(
            "Armário", StorageLocationType.ReagentCabinet, description: "old");

        location.DescribeAs("   ");

        Assert.Null(location.Description);
    }

    [Fact]
    public void Rename_changes_the_name()
    {
        StorageLocation location = NewLocation();

        location.Rename("  Almoxarifado Central  ");

        Assert.Equal("Almoxarifado Central", location.Name);
    }

    [Fact]
    public void Deactivate_takes_the_location_out_of_service_and_raises_an_event()
    {
        StorageLocation location = NewLocation();
        location.ClearDomainEvents();

        location.Deactivate();

        Assert.False(location.IsActive);
        Assert.IsType<StorageLocationDeactivatedEvent>(Assert.Single(location.DomainEvents));
    }

    [Fact]
    public void Deactivate_is_idempotent()
    {
        StorageLocation location = NewLocation();
        location.Deactivate();
        location.ClearDomainEvents();

        location.Deactivate();

        Assert.False(location.IsActive);
        Assert.Empty(location.DomainEvents);
    }

    [Fact]
    public void Reactivate_puts_the_location_back_in_service_and_raises_an_event()
    {
        StorageLocation location = NewLocation();
        location.Deactivate();
        location.ClearDomainEvents();

        location.Reactivate();

        Assert.True(location.IsActive);
        Assert.IsType<StorageLocationReactivatedEvent>(Assert.Single(location.DomainEvents));
    }

    [Fact]
    public void Reactivate_is_idempotent()
    {
        StorageLocation location = NewLocation();
        location.ClearDomainEvents();

        location.Reactivate();

        Assert.True(location.IsActive);
        Assert.Empty(location.DomainEvents);
    }

    [Fact]
    public void CanStore_accepts_a_non_controlled_item_anywhere()
    {
        Assert.True(NewLocation(StorageLocationType.GeneralStorage).CanStore(isControlledItem: false));
    }

    [Fact]
    public void CanStore_accepts_a_controlled_item_only_in_a_controlled_location()
    {
        Assert.True(NewLocation(StorageLocationType.Controlled).CanStore(isControlledItem: true));
        Assert.False(NewLocation(StorageLocationType.GeneralStorage).CanStore(isControlledItem: true));
    }

    [Fact]
    public void EnsureCanStore_blocks_a_controlled_item_in_a_non_controlled_location()
    {
        StorageLocation warehouse = NewLocation(StorageLocationType.GeneralStorage);

        Assert.Throws<DomainException>(() => warehouse.EnsureCanStore(NewItem(controlled: true)));
    }

    [Fact]
    public void EnsureCanStore_accepts_a_controlled_item_in_a_controlled_location()
    {
        StorageLocation box = NewLocation(StorageLocationType.Controlled);

        box.EnsureCanStore(NewItem(controlled: true));
    }

    [Fact]
    public void EnsureCanStore_accepts_a_non_controlled_item_in_a_general_location()
    {
        StorageLocation warehouse = NewLocation(StorageLocationType.GeneralStorage);

        warehouse.EnsureCanStore(NewItem(controlled: false));
    }

    [Fact]
    public void EnsureCanStore_blocks_any_item_in_an_inactive_location()
    {
        StorageLocation box = NewLocation(StorageLocationType.Controlled);
        box.Deactivate();

        Assert.Throws<DomainException>(() => box.EnsureCanStore(NewItem(controlled: true)));
    }
}
