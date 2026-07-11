using SISLAB.Modules.Inventory.Application.StockMovements;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.Modules.Inventory.Tests.Application.Partners;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Application.StockMovements;

public sealed class RegisterStockEntryCommandHandlerTests
{
    private static RegisterStockEntryCommandHandler HandlerFor(
        FakeStockItemRepository items,
        FakePartnerRepository? partners = null)
        => new(items, partners ?? new FakePartnerRepository());

    [Fact]
    public async Task Increases_the_balance_and_persists_the_item()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m);
        var repository = new FakeStockItemRepository().Seed(item);
        RegisterStockEntryCommandHandler handler = HandlerFor(repository);

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
        RegisterStockEntryCommandHandler handler = HandlerFor(new FakeStockItemRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RegisterStockEntryCommand(
                Guid.NewGuid(), 10m, "mL", null, null, null, null, null)));
    }

    [Fact]
    public async Task Applies_the_entry_when_the_informed_supplier_can_supply()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid(), initial: 100m);
        Partner supplier = Partner.Register("Sigma-Aldrich", PartnerType.Supplier);
        var items = new FakeStockItemRepository().Seed(item);
        RegisterStockEntryCommandHandler handler =
            HandlerFor(items, new FakePartnerRepository().Seed(supplier));

        Guid id = await handler.HandleAsync(new RegisterStockEntryCommand(
            item.Id, 50m, "mL", null, null, null, SupplierPartnerId: supplier.Id, OccurredOn: null));

        Assert.Equal(item.Id, id);
        Assert.Equal(Quantity.Of(150m, StockItemFactory.Ml), item.Quantity);
        Assert.Same(item, items.LastUpdated);
    }

    [Fact]
    public async Task Fails_when_the_informed_supplier_does_not_exist()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid());
        RegisterStockEntryCommandHandler handler =
            HandlerFor(new FakeStockItemRepository().Seed(item));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new RegisterStockEntryCommand(
                item.Id, 10m, "mL", null, null, null, SupplierPartnerId: Guid.NewGuid(), OccurredOn: null)));
    }

    [Fact]
    public async Task Fails_when_the_informed_partner_is_not_a_supplier()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid());
        Partner client = Partner.Register("Barbosa—UFBA", PartnerType.Client);
        RegisterStockEntryCommandHandler handler = HandlerFor(
            new FakeStockItemRepository().Seed(item),
            new FakePartnerRepository().Seed(client));

        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new RegisterStockEntryCommand(
                item.Id, 10m, "mL", null, null, null, SupplierPartnerId: client.Id, OccurredOn: null)));
    }

    [Fact]
    public async Task Fails_when_the_informed_supplier_is_inactive()
    {
        StockItem item = StockItemFactory.At(Guid.NewGuid());
        Partner supplier = Partner.Register("Cristália", PartnerType.Both);
        supplier.Deactivate();
        RegisterStockEntryCommandHandler handler = HandlerFor(
            new FakeStockItemRepository().Seed(item),
            new FakePartnerRepository().Seed(supplier));

        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new RegisterStockEntryCommand(
                item.Id, 10m, "mL", null, null, null, SupplierPartnerId: supplier.Id, OccurredOn: null)));
    }
}
