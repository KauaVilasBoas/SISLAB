using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.Modules.Inventory.Application.StockMovements.Commands;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.Modules.Inventory.Tests.Application.Audit;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements.Commands;

public sealed class RegisterConsumptionCommandHandlerTests
{
    [Fact]
    public async Task Decreases_the_balance_and_persists_the_item()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterConsumptionCommandHandler(repository, audit.Recorder);

        await handler.HandleAsync(new RegisterConsumptionCommand(
            item.Id, 30m, "mL", ExperimentId: Guid.NewGuid(), OccurredOn: null));

        Assert.Equal(Quantity.Of(70m, StockItemFactory.Ml), item.Quantity);
        Assert.Same(item, repository.LastUpdated);
    }

    [Fact]
    public async Task Does_not_audit_when_item_is_not_controlled()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m, isControlled: false);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterConsumptionCommandHandler(repository, audit.Recorder);

        await handler.HandleAsync(new RegisterConsumptionCommand(item.Id, 30m, "mL", null, null));

        Assert.Empty(audit.Writer.Entries);
    }

    [Fact]
    public async Task Audits_the_consumption_when_item_is_controlled()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m, isControlled: true);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create(actor: "user-42");
        var handler = new RegisterConsumptionCommandHandler(repository, audit.Recorder);

        await handler.HandleAsync(new RegisterConsumptionCommand(item.Id, 30m, "mL", null, null));

        Assert.Single(audit.Writer.Entries);
        var entry = audit.Writer.LastEntry!;
        Assert.Equal(InventoryAuditActions.Consumption, entry.Action);
        Assert.Equal("StockItem", entry.EntityType);
        Assert.Equal(item.Id, entry.EntityId);
        Assert.Equal(item.CompanyId, entry.CompanyId);
        Assert.Equal("user-42", entry.UserId);
        Assert.Contains("30", entry.Payload);
    }

    [Fact]
    public async Task Fails_when_the_amount_exceeds_the_balance()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 10m);
        var repository = new FakeStockItemRepository().Seed(item);
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterConsumptionCommandHandler(repository, audit.Recorder);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(
            new RegisterConsumptionCommand(item.Id, 50m, "mL", null, null)));
    }

    [Fact]
    public async Task Fails_when_the_item_does_not_exist()
    {
        TestAuditRecorder audit = TestAuditRecorder.Create();
        var handler = new RegisterConsumptionCommandHandler(new FakeStockItemRepository(), audit.Recorder);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RegisterConsumptionCommand(Guid.NewGuid(), 1m, "mL", null, null)));
    }
}
