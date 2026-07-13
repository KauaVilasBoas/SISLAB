using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.Modules.Inventory.Application.StockMovements.Commands;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.Modules.Inventory.Tests.Application.Audit;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

public sealed class DisposeStockCommandHandlerTests
{
    [Fact]
    public async Task Decreases_the_balance_raises_the_event_and_persists_the_item()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new DisposeStockCommandHandler(repository, audit.Recorder);

        await handler.HandleAsync(new DisposeStockCommand(item.Id, 40m, "mL", "expired", null));

        Assert.Equal(Quantity.Of(60m, StockItemFactory.Ml), item.Quantity);
        Assert.Contains(item.DomainEvents, e => e is StockDisposedEvent);
        Assert.Same(item, repository.LastUpdated);
    }

    [Fact]
    public async Task Does_not_audit_when_item_is_not_controlled()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m, isControlled: false);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new DisposeStockCommandHandler(repository, audit.Recorder);

        await handler.HandleAsync(new DisposeStockCommand(item.Id, 40m, "mL", "expired", null));

        Assert.Empty(audit.Writer.Entries);
    }

    [Fact]
    public async Task Audits_the_disposal_with_reason_when_item_is_controlled()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m, isControlled: true);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new DisposeStockCommandHandler(repository, audit.Recorder);

        await handler.HandleAsync(new DisposeStockCommand(item.Id, 40m, "mL", "expired batch", null));

        Assert.Single(audit.Writer.Entries);
        var entry = audit.Writer.LastEntry!;
        Assert.Equal(InventoryAuditActions.Disposal, entry.Action);
        Assert.Equal(item.Id, entry.EntityId);
        Assert.Contains("expired batch", entry.Payload);
    }

    [Fact]
    public async Task Fails_when_the_amount_exceeds_the_balance()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 10m);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new DisposeStockCommandHandler(
            new FakeStockItemRepository().Seed(item), audit.Recorder);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new DisposeStockCommand(item.Id, 50m, "mL", "expired", null)));
    }
}
