using SISLAB.Modules.Inventory.Application.StockMovements;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements;

public sealed class DisposeStockCommandHandlerTests
{
    [Fact]
    public async Task Decreases_the_balance_raises_the_event_and_persists_the_item()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m);
        var repository = new FakeStockItemRepository().Seed(item);
        var handler = new DisposeStockCommandHandler(repository);

        await handler.HandleAsync(new DisposeStockCommand(item.Id, 40m, "mL", "expired", null));

        Assert.Equal(Quantity.Of(60m, StockItemFactory.Ml), item.Quantity);
        Assert.Contains(item.DomainEvents, e => e is StockDisposedEvent);
        Assert.Same(item, repository.LastUpdated);
    }

    [Fact]
    public async Task Fails_when_the_amount_exceeds_the_balance()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 10m);
        var handler = new DisposeStockCommandHandler(new FakeStockItemRepository().Seed(item));

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new DisposeStockCommand(item.Id, 50m, "mL", "expired", null)));
    }
}
