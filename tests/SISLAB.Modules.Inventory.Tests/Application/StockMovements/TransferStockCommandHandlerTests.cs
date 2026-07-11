using SISLAB.Modules.Inventory.Application.StockMovements;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements;

public sealed class TransferStockCommandHandlerTests
{
    [Fact]
    public async Task Moves_the_item_to_the_destination_and_persists_it()
    {
        var origin = Guid.NewGuid();
        StorageLocation destination = StorageLocation.Register("Cabinet B", StorageLocationType.GeneralStorage);
        StockItem item = StockItemFactory.At(origin);

        var items = new FakeStockItemRepository().Seed(item);
        var locations = new FakeStorageLocationRepository().Seed(destination);
        var handler = new TransferStockCommandHandler(items, locations);

        await handler.HandleAsync(new TransferStockCommand(item.Id, origin, destination.Id, null));

        Assert.Equal(destination.Id, item.StorageLocationId);
        Assert.Same(item, items.LastUpdated);
    }

    [Fact]
    public async Task Allows_a_controlled_item_into_a_controlled_location()
    {
        var origin = Guid.NewGuid();
        StorageLocation destination = StorageLocation.Register("Controlled box", StorageLocationType.Controlled);
        StockItem controlled = StockItemFactory.At(origin, isControlled: true);

        var items = new FakeStockItemRepository().Seed(controlled);
        var handler = new TransferStockCommandHandler(
            items,
            new FakeStorageLocationRepository().Seed(destination));

        await handler.HandleAsync(new TransferStockCommand(controlled.Id, origin, destination.Id, null));

        Assert.Equal(destination.Id, controlled.StorageLocationId);
        Assert.Same(controlled, items.LastUpdated);
    }

    [Fact]
    public async Task Rejects_a_controlled_item_into_a_non_controlled_location()
    {
        var origin = Guid.NewGuid();
        StorageLocation destination = StorageLocation.Register("Fridge", StorageLocationType.GeneralStorage);
        StockItem controlled = StockItemFactory.At(origin, isControlled: true);

        var handler = new TransferStockCommandHandler(
            new FakeStockItemRepository().Seed(controlled),
            new FakeStorageLocationRepository().Seed(destination));

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new TransferStockCommand(controlled.Id, origin, destination.Id, null)));
    }

    [Fact]
    public async Task Rejects_a_mismatched_origin_location()
    {
        StorageLocation destination = StorageLocation.Register("Cabinet B", StorageLocationType.GeneralStorage);
        StockItem item = StockItemFactory.At(Guid.NewGuid());

        var handler = new TransferStockCommandHandler(
            new FakeStockItemRepository().Seed(item),
            new FakeStorageLocationRepository().Seed(destination));

        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new TransferStockCommand(item.Id, Guid.NewGuid(), destination.Id, null)));
    }

    [Fact]
    public async Task Rejects_an_inactive_destination()
    {
        var origin = Guid.NewGuid();
        StorageLocation destination = StorageLocation.Register("Cabinet B", StorageLocationType.GeneralStorage);
        destination.Deactivate();
        StockItem item = StockItemFactory.At(origin);

        var handler = new TransferStockCommandHandler(
            new FakeStockItemRepository().Seed(item),
            new FakeStorageLocationRepository().Seed(destination));

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new TransferStockCommand(item.Id, origin, destination.Id, null)));
    }
}
