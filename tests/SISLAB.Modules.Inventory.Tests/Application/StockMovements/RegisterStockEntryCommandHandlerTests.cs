using SISLAB.Modules.Inventory.Application.StockMovements;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements;

public sealed class RegisterStockEntryCommandHandlerTests
{
    [Fact]
    public async Task Increases_the_balance_and_persists_the_item()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m);
        var repository = new FakeStockItemRepository().Seed(item);
        var handler = new RegisterStockEntryCommandHandler(repository);

        Guid id = await handler.HandleAsync(new RegisterStockEntryCommand(
            item.Id, 50m, "mL", LotCode: "BATCH-9", ExpiryYear: 2028, ExpiryMonth: 3,
            SupplierPartnerId: null, OccurredOn: null));

        Assert.Equal(item.Id, id);
        Assert.Equal(Quantity.Of(150m, StockItemFactory.Ml), item.Quantity);
        Assert.Same(item, repository.LastUpdated);
    }

    [Fact]
    public async Task Fails_when_the_item_does_not_exist()
    {
        var handler = new RegisterStockEntryCommandHandler(new FakeStockItemRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RegisterStockEntryCommand(
                Guid.NewGuid(), 10m, "mL", null, null, null, null, null)));
    }
}
