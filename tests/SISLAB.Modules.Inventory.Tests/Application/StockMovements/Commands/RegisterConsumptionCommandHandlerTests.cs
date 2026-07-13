using SISLAB.Modules.Inventory.Application.StockMovements.Commands;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

public sealed class RegisterConsumptionCommandHandlerTests
{
    [Fact]
    public async Task Decreases_the_balance_and_persists_the_item()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m);
        var repository = new FakeStockItemRepository().Seed(item);
        var handler = new RegisterConsumptionCommandHandler(repository);

        await handler.HandleAsync(new RegisterConsumptionCommand(
            item.Id, 30m, "mL", ExperimentId: Guid.NewGuid(), OccurredOn: null));

        Assert.Equal(Quantity.Of(70m, StockItemFactory.Ml), item.Quantity);
        Assert.Same(item, repository.LastUpdated);
    }

    [Fact]
    public async Task Fails_when_the_amount_exceeds_the_balance()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 10m);
        var repository = new FakeStockItemRepository().Seed(item);
        var handler = new RegisterConsumptionCommandHandler(repository);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new RegisterConsumptionCommand(item.Id, 50m, "mL", null, null)));
    }

    [Fact]
    public async Task Fails_when_the_item_does_not_exist()
    {
        var handler = new RegisterConsumptionCommandHandler(new FakeStockItemRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RegisterConsumptionCommand(Guid.NewGuid(), 1m, "mL", null, null)));
    }
}
