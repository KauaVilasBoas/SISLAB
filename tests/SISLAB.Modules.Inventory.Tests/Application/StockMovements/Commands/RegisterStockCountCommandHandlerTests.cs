using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.Modules.Inventory.Application.StockMovements.Commands;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.Modules.Inventory.Tests.Application.Audit;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

public sealed class RegisterStockCountCommandHandlerTests
{
    [Fact]
    public async Task Records_the_divergence_without_changing_the_balance()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m, isControlled: true);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterStockCountCommandHandler(repository, audit.Recorder);

        decimal divergence = await handler.HandleAsync(
            new RegisterStockCountCommand(item.Id, 95m, "mL", null));

        Assert.Equal(-5m, divergence);
        Assert.Equal(Quantity.Of(100m, StockItemFactory.Ml), item.Quantity);

        StockCountedEvent counted = Assert.IsType<StockCountedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(-5m, counted.Divergence);
        Assert.Equal(Quantity.Of(95m, StockItemFactory.Ml), counted.CountedQuantity);
        Assert.Same(item, repository.LastUpdated);
    }

    [Fact]
    public async Task Audits_the_count_of_a_controlled_item()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m, isControlled: true);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterStockCountCommandHandler(repository, audit.Recorder);

        await handler.HandleAsync(new RegisterStockCountCommand(item.Id, 95m, "mL", null));

        Assert.Single(audit.Writer.Entries);
        Assert.Equal(InventoryAuditActions.StockCount, audit.Writer.LastEntry!.Action);
    }

    [Fact]
    public async Task Records_a_zero_divergence_when_the_count_matches()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m, isControlled: true);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterStockCountCommandHandler(
            new FakeStockItemRepository().Seed(item), audit.Recorder);

        decimal divergence = await handler.HandleAsync(
            new RegisterStockCountCommand(item.Id, 100m, "mL", null));

        Assert.Equal(0m, divergence);
    }

    [Fact]
    public async Task Fails_when_the_item_does_not_exist()
    {
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterStockCountCommandHandler(new FakeStockItemRepository(), audit.Recorder);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RegisterStockCountCommand(Guid.NewGuid(), 1m, "mL", null)));
    }
}
