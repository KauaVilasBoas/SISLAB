using SISLAB.Modules.Inventory.Application.StockMovements.Commands;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Tests.Application.Configuration;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

/// <summary>
/// Covers <see cref="UpdateStockItemCommandHandler"/> (card [E7] #46): the conservative metadata edit. It loads
/// the aggregate (404 if unknown), re-validates both cross-module references the create command validates — the
/// category (through <c>ILabConfiguration</c>) and the storage location — and then applies name/category/location/
/// minimum/brand/application through the aggregate's behaviour methods, reusing the item's fixed unit for the new
/// minimum. It never touches the balance, lot or expiry, and hands the aggregate back for persistence.
/// </summary>
public sealed class UpdateStockItemCommandHandlerTests
{
    private static readonly Guid Category = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OtherCategory = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    private static UpdateStockItemCommandHandler HandlerFor(
        FakeStockItemRepository items,
        FakeStorageLocationRepository locations,
        FakeLabConfiguration labConfiguration)
        => new(items, locations, labConfiguration);

    [Fact]
    public async Task Updates_the_metadata_and_persists_the_item()
    {
        StorageLocation origin = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        StorageLocation destination = StorageLocation.Register("Bancada", StorageLocationType.GeneralStorage);
        StockItem item = StockItemFactory.At(origin.Id, initial: 100m, minimum: 10m);

        var items = new FakeStockItemRepository().Seed(item);
        UpdateStockItemCommandHandler handler = HandlerFor(
            items,
            new FakeStorageLocationRepository().Seed(origin).Seed(destination),
            new FakeLabConfiguration().WithCategory(Category).WithCategory(OtherCategory));

        await handler.HandleAsync(new UpdateStockItemCommand(
            StockItemId: item.Id,
            Name: "DMSO anidro",
            CategoryId: OtherCategory,
            StorageLocationId: destination.Id,
            MinimumQuantity: 25m,
            Brand: "Sigma",
            Application: "Uso em bancada"));

        StockItem persisted = items.LastUpdated!;
        Assert.Equal("DMSO anidro", persisted.Name);
        Assert.Equal(OtherCategory, persisted.CategoryId);
        Assert.Equal(destination.Id, persisted.StorageLocationId);
        Assert.Equal(25m, persisted.MinimumQuantity.Value);
        Assert.Equal(StockItemFactory.Ml, persisted.MinimumQuantity.Unit);
        Assert.Equal("Sigma", persisted.Brand);
        Assert.Equal("Uso em bancada", persisted.Application);
    }

    [Fact]
    public async Task Does_not_touch_the_balance_lot_or_expiry()
    {
        StorageLocation location = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        StockItem item = StockItemFactory.At(location.Id, initial: 100m, minimum: 10m);

        var items = new FakeStockItemRepository().Seed(item);
        UpdateStockItemCommandHandler handler = HandlerFor(
            items,
            new FakeStorageLocationRepository().Seed(location),
            new FakeLabConfiguration().WithCategory(Category));

        await handler.HandleAsync(new UpdateStockItemCommand(
            item.Id, "DMSO", Category, location.Id, MinimumQuantity: 5m, Brand: null, Application: null));

        StockItem persisted = items.LastUpdated!;
        // The metadata edit never touches the batch ledger: the balance and the batches' lot/expiry are intact.
        Assert.Equal(100m, persisted.Quantity.Value);
        StockBatch batch = Assert.Single(persisted.Batches);
        Assert.Null(batch.Lot);
        Assert.Null(batch.Expiry);
    }

    [Fact]
    public async Task Fails_when_the_item_does_not_exist()
    {
        StorageLocation location = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        UpdateStockItemCommandHandler handler = HandlerFor(
            new FakeStockItemRepository(),
            new FakeStorageLocationRepository().Seed(location),
            new FakeLabConfiguration().WithCategory(Category));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new UpdateStockItemCommand(
            Guid.NewGuid(), "DMSO", Category, location.Id, 5m, null, null)));
    }

    [Fact]
    public async Task Fails_when_the_new_category_does_not_exist_for_the_tenant()
    {
        StorageLocation location = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        StockItem item = StockItemFactory.At(location.Id);

        UpdateStockItemCommandHandler handler = HandlerFor(
            new FakeStockItemRepository().Seed(item),
            new FakeStorageLocationRepository().Seed(location),
            new FakeLabConfiguration()); // no category registered

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new UpdateStockItemCommand(
            item.Id, "DMSO", Category, location.Id, 5m, null, null)));
    }

    [Fact]
    public async Task Fails_when_the_new_storage_location_does_not_exist()
    {
        StorageLocation location = StorageLocation.Register("Geladeira", StorageLocationType.Refrigerated);
        StockItem item = StockItemFactory.At(location.Id);

        UpdateStockItemCommandHandler handler = HandlerFor(
            new FakeStockItemRepository().Seed(item),
            new FakeStorageLocationRepository().Seed(location),
            new FakeLabConfiguration().WithCategory(Category));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new UpdateStockItemCommand(
            item.Id, "DMSO", Category, Guid.NewGuid(), 5m, null, null)));
    }
}
