using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Tests.Domain.StockItems;

public sealed class StockItemTests
{
    private static readonly UnitOfMeasure Ml = UnitOfMeasure.Milliliter;
    private static readonly Guid Location = Guid.NewGuid();

    private static StockItem NewItem(decimal initial = 100m, decimal minimum = 10m) =>
        StockItem.Register(
            name: "DMSO",
            category: StockItemCategory.Solvent,
            storageLocationId: Location,
            initialQuantity: Quantity.Of(initial, Ml),
            minimumQuantity: Quantity.Of(minimum, Ml));

    [Fact]
    public void Register_captures_all_descriptive_attributes()
    {
        StockItem item = StockItem.Register(
            name: "Cetamina 10%",
            category: StockItemCategory.ControlledAnesthetic,
            storageLocationId: Location,
            initialQuantity: Quantity.Of(5m, UnitOfMeasure.Ampoule),
            minimumQuantity: Quantity.Of(2m, UnitOfMeasure.Ampoule),
            isControlled: true,
            containerState: ContainerState.Closed,
            brand: "Cristália",
            application: "Anestesia",
            lot: Lot.FromCode("L-2026-01"),
            expiry: ExpiryDate.FromYearMonth(2027, 6));

        Assert.Equal("Cetamina 10%", item.Name);
        Assert.Equal(StockItemCategory.ControlledAnesthetic, item.Category);
        Assert.Equal("Cristália", item.Brand);
        Assert.Equal(ContainerState.Closed, item.ContainerState);
        Assert.Equal("Anestesia", item.Application);
        Assert.True(item.IsControlled);
        Assert.Equal("L-2026-01", item.Lot!.Code);
        Assert.Equal(ExpiryDate.FromYearMonth(2027, 6), item.Expiry);
        Assert.Equal(Quantity.Of(5m, UnitOfMeasure.Ampoule), item.Quantity);
    }

    [Fact]
    public void Register_rejects_a_minimum_in_a_different_unit()
    {
        Assert.Throws<DomainException>(() => StockItem.Register(
            name: "MTT",
            category: StockItemCategory.Reagent,
            storageLocationId: Location,
            initialQuantity: Quantity.Of(1m, UnitOfMeasure.Gram),
            minimumQuantity: Quantity.Of(1m, UnitOfMeasure.Milliliter)));
    }

    [Fact]
    public void StockItem_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(NewItem());
    }

    [Fact]
    public void RegisterEntry_increases_the_balance_and_updates_lot_and_expiry()
    {
        StockItem item = NewItem(initial: 100m);
        Lot lot = Lot.FromCode("BATCH-42")!;
        ExpiryDate expiry = ExpiryDate.FromYearMonth(2028, 3);

        item.RegisterEntry(Quantity.Of(50m, Ml), lot, expiry);

        Assert.Equal(Quantity.Of(150m, Ml), item.Quantity);
        Assert.Equal(lot, item.Lot);
        Assert.Equal(expiry, item.Expiry);
    }

    [Fact]
    public void RegisterEntry_raises_StockReceived_with_the_resulting_balance()
    {
        StockItem item = NewItem(initial: 100m);

        item.RegisterEntry(Quantity.Of(20m, Ml));

        StockReceivedEvent received = Assert.IsType<StockReceivedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(Quantity.Of(20m, Ml), received.ReceivedQuantity);
        Assert.Equal(Quantity.Of(120m, Ml), received.ResultingQuantity);
    }

    [Fact]
    public void RegisterConsumption_decreases_the_balance_and_raises_StockConsumed()
    {
        StockItem item = NewItem(initial: 100m);

        item.RegisterConsumption(Quantity.Of(30m, Ml));

        Assert.Equal(Quantity.Of(70m, Ml), item.Quantity);
        StockConsumedEvent consumed = Assert.IsType<StockConsumedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(Quantity.Of(70m, Ml), consumed.ResultingQuantity);
    }

    [Fact]
    public void RegisterConsumption_fails_when_it_exceeds_the_balance()
    {
        StockItem item = NewItem(initial: 20m);

        Assert.Throws<DomainException>(() => item.RegisterConsumption(Quantity.Of(21m, Ml)));
        Assert.Equal(Quantity.Of(20m, Ml), item.Quantity);
        Assert.Empty(item.DomainEvents);
    }

    [Fact]
    public void Consuming_the_whole_balance_leaves_a_zero_never_negative_quantity()
    {
        StockItem item = NewItem(initial: 20m);

        item.RegisterConsumption(Quantity.Of(20m, Ml));

        Assert.True(item.Quantity.IsZero);
    }

    [Fact]
    public void Operations_reject_a_zero_quantity()
    {
        StockItem item = NewItem();

        Assert.Throws<DomainException>(() => item.RegisterConsumption(Quantity.Zero(Ml)));
    }

    [Fact]
    public void Operations_reject_a_quantity_in_a_different_unit()
    {
        StockItem item = NewItem();

        Assert.Throws<DomainException>(() => item.RegisterConsumption(Quantity.Of(1m, UnitOfMeasure.Gram)));
    }

    [Fact]
    public void TransferTo_moves_the_item_and_raises_StockTransferred()
    {
        StockItem item = NewItem();
        Guid destination = Guid.NewGuid();

        item.TransferTo(destination);

        Assert.Equal(destination, item.StorageLocationId);
        StockTransferredEvent transferred = Assert.IsType<StockTransferredEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(Location, transferred.FromStorageLocationId);
        Assert.Equal(destination, transferred.ToStorageLocationId);
    }

    [Fact]
    public void TransferTo_the_same_location_is_rejected()
    {
        StockItem item = NewItem();

        Assert.Throws<DomainException>(() => item.TransferTo(Location));
        Assert.Empty(item.DomainEvents);
    }

    [Fact]
    public void Dispose_decreases_the_balance_and_raises_StockDisposed()
    {
        StockItem item = NewItem(initial: 40m);

        item.Dispose(Quantity.Of(15m, Ml));

        Assert.Equal(Quantity.Of(25m, Ml), item.Quantity);
        Assert.IsType<StockDisposedEvent>(Assert.Single(item.DomainEvents));
    }

    [Fact]
    public void Dispose_fails_when_it_exceeds_the_balance()
    {
        StockItem item = NewItem(initial: 5m);

        Assert.Throws<DomainException>(() => item.Dispose(Quantity.Of(6m, Ml)));
    }

    [Fact]
    public void Consuming_an_expired_item_is_allowed_not_blocked()
    {
        StockItem item = StockItem.Register(
            name: "DMSO",
            category: StockItemCategory.Solvent,
            storageLocationId: Location,
            initialQuantity: Quantity.Of(100m, Ml),
            minimumQuantity: Quantity.Of(10m, Ml),
            expiry: ExpiryDate.FromYearMonth(2020, 1));

        item.RegisterConsumption(Quantity.Of(10m, Ml));

        Assert.Equal(Quantity.Of(90m, Ml), item.Quantity);
    }

    [Fact]
    public void ClassifyExpiry_flags_an_expired_item_for_alerting()
    {
        StockItem item = StockItem.Register(
            name: "DMSO",
            category: StockItemCategory.Solvent,
            storageLocationId: Location,
            initialQuantity: Quantity.Of(100m, Ml),
            minimumQuantity: Quantity.Of(10m, Ml),
            expiry: ExpiryDate.FromYearMonth(2020, 1));

        ExpiryStatus? status = item.ClassifyExpiry(FixedClock.On(2026, 7, 11), TimeSpan.FromDays(30));

        Assert.Equal(ExpiryStatus.Expired, status);
    }

    [Fact]
    public void ClassifyExpiry_returns_null_for_an_item_without_expiry()
    {
        StockItem item = NewItem();

        Assert.Null(item.ClassifyExpiry(FixedClock.On(2026, 7, 11), TimeSpan.FromDays(30)));
    }

    [Fact]
    public void IsBelowMinimum_reflects_the_balance_against_the_threshold()
    {
        StockItem item = NewItem(initial: 12m, minimum: 10m);
        Assert.False(item.IsBelowMinimum);

        item.RegisterConsumption(Quantity.Of(5m, Ml));

        Assert.True(item.IsBelowMinimum);
    }

    [Fact]
    public void RegisterConsumption_raises_StockBelowMinimum_when_it_crosses_the_threshold()
    {
        StockItem item = NewItem(initial: 12m, minimum: 10m);

        item.RegisterConsumption(Quantity.Of(5m, Ml));

        StockBelowMinimumEvent belowMinimum = Assert.Single(item.DomainEvents.OfType<StockBelowMinimumEvent>());
        Assert.Equal(item.Id, belowMinimum.StockItemId);
        Assert.Equal(Quantity.Of(7m, Ml), belowMinimum.CurrentQuantity);
        Assert.Equal(Quantity.Of(10m, Ml), belowMinimum.MinimumQuantity);
    }

    [Fact]
    public void RegisterConsumption_does_not_raise_StockBelowMinimum_while_still_above_the_threshold()
    {
        StockItem item = NewItem(initial: 100m, minimum: 10m);

        item.RegisterConsumption(Quantity.Of(30m, Ml));

        Assert.Empty(item.DomainEvents.OfType<StockBelowMinimumEvent>());
    }

    [Fact]
    public void RegisterConsumption_raises_StockBelowMinimum_only_once_on_the_crossing_not_on_every_consumption()
    {
        StockItem item = NewItem(initial: 12m, minimum: 10m);

        item.RegisterConsumption(Quantity.Of(5m, Ml)); // 12 -> 7: crosses
        item.RegisterConsumption(Quantity.Of(2m, Ml)); // 7 -> 5: already below, no new signal

        Assert.Single(item.DomainEvents.OfType<StockBelowMinimumEvent>());
    }

    [Fact]
    public void A_new_item_starts_with_no_domain_events()
    {
        Assert.Empty(NewItem().DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_collection()
    {
        StockItem item = NewItem();
        item.RegisterConsumption(Quantity.Of(1m, Ml));

        item.ClearDomainEvents();

        Assert.Empty(item.DomainEvents);
    }
}
