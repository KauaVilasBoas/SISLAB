using SISLAB.Modules.Inventory.Application.StockMovements.Commands;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Tests.Application.Configuration;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

/// <summary>
/// Covers the write-side guards of <see cref="RegisterStockItemCommandHandler"/> (card [E12] #76): the item's
/// category is referenced by value, so before the aggregate is created the handler validates both cross-module
/// references — the category exists for the active tenant (through the Configuration boundary,
/// <c>ILabConfiguration</c>) and the storage location exists — mirroring the supplier guard on the entry command.
/// An unknown category or location is a <see cref="NotFoundException"/>; only a valid pair persists the item.
/// </summary>
public sealed class RegisterStockItemCommandHandlerTests
{
    private static readonly Guid Category = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static RegisterStockItemCommandHandler HandlerFor(
        FakeStockItemRepository items,
        FakeStorageLocationRepository locations,
        FakeLabConfiguration labConfiguration)
        => new(items, locations, labConfiguration);

    private static RegisterStockItemCommand CommandFor(Guid categoryId, Guid storageLocationId) =>
        new(
            Name: "DMSO",
            CategoryId: categoryId,
            StorageLocationId: storageLocationId,
            InitialQuantity: 100m,
            MinimumQuantity: 10m,
            Unit: "mL",
            IsControlled: false,
            Brand: null,
            Application: null,
            LotCode: null,
            ExpiryYear: null,
            ExpiryMonth: null);

    [Fact]
    public async Task Registers_the_item_when_the_category_and_location_exist()
    {
        StorageLocation location = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        var items = new FakeStockItemRepository();
        RegisterStockItemCommandHandler handler = HandlerFor(
            items,
            new FakeStorageLocationRepository().Seed(location),
            new FakeLabConfiguration().WithCategory(Category));

        Guid id = await handler.HandleAsync(CommandFor(Category, location.Id));

        StockItem? persisted = await items.FindByIdAsync(id);
        Assert.NotNull(persisted);
        Assert.Equal(Category, persisted!.CategoryId);
        Assert.Equal(location.Id, persisted.StorageLocationId);
    }

    [Fact]
    public async Task Fails_when_the_category_does_not_exist_for_the_tenant()
    {
        StorageLocation location = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        RegisterStockItemCommandHandler handler = HandlerFor(
            new FakeStockItemRepository(),
            new FakeStorageLocationRepository().Seed(location),
            new FakeLabConfiguration()); // no category registered

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(CommandFor(Category, location.Id)));
    }

    [Fact]
    public async Task Fails_when_the_storage_location_does_not_exist()
    {
        RegisterStockItemCommandHandler handler = HandlerFor(
            new FakeStockItemRepository(),
            new FakeStorageLocationRepository(), // no location seeded
            new FakeLabConfiguration().WithCategory(Category));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(CommandFor(Category, Guid.NewGuid())));
    }
}
