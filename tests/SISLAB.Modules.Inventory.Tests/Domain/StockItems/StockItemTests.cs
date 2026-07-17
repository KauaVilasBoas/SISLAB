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

    // The category is referenced by value (a per-tenant Configuration category, card [E12] #76); the aggregate
    // only holds the id, so a fixed Guid is all these fixtures need.
    private static readonly Guid Category = Guid.NewGuid();

    private static StockItem NewItem(decimal initial = 100m, decimal minimum = 10m) =>
        StockItem.Register(
            name: "DMSO",
            categoryId: Category,
            storageLocationId: Location,
            initialQuantity: Quantity.Of(initial, Ml),
            minimumQuantity: Quantity.Of(minimum, Ml));

    [Fact]
    public void Register_captures_all_descriptive_attributes()
    {
        Guid category = Guid.NewGuid();

        StockItem item = StockItem.Register(
            name: "Cetamina 10%",
            categoryId: category,
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
        Assert.Equal(category, item.CategoryId);
        Assert.Equal("Cristália", item.Brand);
        Assert.Equal(ContainerState.Closed, item.ContainerState);
        Assert.Equal("Anestesia", item.Application);
        Assert.True(item.IsControlled);
        // Lot and expiry now live on the item's first batch (the opening receipt), not the aggregate root.
        StockBatch opening = Assert.Single(item.Batches);
        Assert.Equal("L-2026-01", opening.Lot!.Code);
        Assert.Equal(ExpiryDate.FromYearMonth(2027, 6), opening.Expiry);
        Assert.Equal(Quantity.Of(5m, UnitOfMeasure.Ampoule), item.Quantity);
    }

    [Fact]
    public void Register_rejects_a_minimum_in_a_different_unit()
    {
        Assert.Throws<DomainException>(() => StockItem.Register(
            name: "MTT",
            categoryId: Category,
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
    public void Rename_and_Describe_and_Recategorize_update_the_metadata_without_a_movement()
    {
        StockItem item = NewItem();
        Guid newCategory = Guid.NewGuid();

        item.Rename("DMSO anidro");
        item.Recategorize(newCategory);
        item.Describe(brand: "Sigma", application: "Uso em bancada");

        Assert.Equal("DMSO anidro", item.Name);
        Assert.Equal(newCategory, item.CategoryId);
        Assert.Equal("Sigma", item.Brand);
        Assert.Equal("Uso em bancada", item.Application);
        Assert.Empty(item.DomainEvents);
    }

    [Fact]
    public void Describe_clears_the_metadata_when_passed_blank_values()
    {
        StockItem item = StockItem.Register(
            name: "DMSO",
            categoryId: Category,
            storageLocationId: Location,
            initialQuantity: Quantity.Of(100m, Ml),
            minimumQuantity: Quantity.Of(10m, Ml),
            brand: "Sigma",
            application: "Uso em bancada");

        item.Describe(brand: "   ", application: null);

        Assert.Null(item.Brand);
        Assert.Null(item.Application);
    }

    [Fact]
    public void AdjustMinimumQuantity_updates_the_threshold_and_emits_no_low_stock_event()
    {
        StockItem item = NewItem(initial: 100m, minimum: 10m);

        item.AdjustMinimumQuantity(Quantity.Of(40m, Ml));

        Assert.Equal(Quantity.Of(40m, Ml), item.MinimumQuantity);
        Assert.Empty(item.DomainEvents);
    }

    [Fact]
    public void AdjustMinimumQuantity_rejects_a_threshold_in_a_different_unit()
    {
        StockItem item = NewItem();

        Assert.Throws<DomainException>(() => item.AdjustMinimumQuantity(Quantity.Of(1m, UnitOfMeasure.Gram)));
    }

    [Fact]
    public void Relocate_moves_the_item_without_emitting_a_transfer_movement()
    {
        StockItem item = NewItem();
        Guid destination = Guid.NewGuid();

        item.Relocate(destination);

        Assert.Equal(destination, item.StorageLocationId);
        Assert.Empty(item.DomainEvents);
    }

    [Fact]
    public void Rename_rejects_a_blank_name()
    {
        StockItem item = NewItem();

        Assert.Throws<DomainException>(() => item.Rename("   "));
    }

    [Fact]
    public void RegisterEntry_adds_a_new_batch_with_its_lot_and_expiry_and_increases_the_balance()
    {
        StockItem item = NewItem(initial: 100m);
        Lot lot = Lot.FromCode("BATCH-42")!;
        ExpiryDate expiry = ExpiryDate.FromYearMonth(2028, 3);

        item.RegisterEntry(Quantity.Of(50m, Ml), lot, expiry);

        // A receipt creates a new batch (it does not merge into the existing one); the balance is the sum.
        Assert.Equal(Quantity.Of(150m, Ml), item.Quantity);
        Assert.Equal(2, item.Batches.Count);
        StockBatch received = item.Batches.Single(batch => Equals(batch.Lot, lot));
        Assert.Equal(expiry, received.Expiry);
        Assert.Equal(Quantity.Of(50m, Ml), received.RemainingQuantity);
    }

    [Fact]
    public void RegisterEntry_carries_the_batch_and_unit_cost_on_StockReceived()
    {
        StockItem item = NewItem(initial: 100m);

        item.RegisterEntry(Quantity.Of(50m, Ml), unitCostBrl: 12.50m);

        StockReceivedEvent received = Assert.IsType<StockReceivedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(12.50m, received.UnitCostBrl);
        StockBatch batch = item.Batches.Single(b => b.UnitCostBrl == 12.50m);
        Assert.Equal(batch.Id, received.BatchId);
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
    public void RegisterEntry_carries_occurred_on_and_supplier_on_StockReceived_for_the_read_model()
    {
        StockItem item = NewItem(initial: 100m);
        Guid supplier = Guid.NewGuid();
        var occurredOn = new DateOnly(2026, 7, 10);

        item.RegisterEntry(Quantity.Of(20m, Ml), occurredOn: occurredOn, supplierPartnerId: supplier);

        StockReceivedEvent received = Assert.IsType<StockReceivedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(occurredOn, received.OccurredOn);
        Assert.Equal(supplier, received.SupplierPartnerId);
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
    public void RegisterConsumption_carries_occurred_on_and_experiment_on_StockConsumed_for_the_read_model()
    {
        StockItem item = NewItem(initial: 100m);
        Guid experiment = Guid.NewGuid();
        var occurredOn = new DateOnly(2026, 7, 9);

        item.RegisterConsumption(Quantity.Of(30m, Ml), occurredOn, experiment);

        StockConsumedEvent consumed = Assert.IsType<StockConsumedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(occurredOn, consumed.OccurredOn);
        Assert.Equal(experiment, consumed.ExperimentId);
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
            categoryId: Category,
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
            categoryId: Category,
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

    // ---- Batch model / FEFO (card [E4] #109 / #111) ------------------------------------------------------

    /// <summary>Registers a zero-balance item (no opening batch) so batches can be added deterministically.</summary>
    private static StockItem EmptyItem(decimal minimum = 0m) =>
        StockItem.Register(
            name: "DMSO",
            categoryId: Category,
            storageLocationId: Location,
            initialQuantity: Quantity.Zero(Ml),
            minimumQuantity: Quantity.Of(minimum, Ml));

    [Fact]
    public void Register_with_a_zero_opening_balance_creates_no_batch()
    {
        StockItem item = EmptyItem();

        Assert.Empty(item.Batches);
        Assert.True(item.Quantity.IsZero);
    }

    [Fact]
    public void Consumption_draws_the_earliest_expiring_batch_first_fefo()
    {
        StockItem item = EmptyItem();
        item.RegisterEntry(Quantity.Of(30m, Ml), Lot.FromCode("LATE"), ExpiryDate.FromYearMonth(2028, 12));
        item.RegisterEntry(Quantity.Of(30m, Ml), Lot.FromCode("EARLY"), ExpiryDate.FromYearMonth(2027, 1));
        item.ClearDomainEvents();

        item.RegisterConsumption(Quantity.Of(10m, Ml));

        StockBatch early = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("EARLY")));
        StockBatch late = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("LATE")));
        Assert.Equal(Quantity.Of(20m, Ml), early.RemainingQuantity); // drawn first
        Assert.Equal(Quantity.Of(30m, Ml), late.RemainingQuantity);  // untouched
    }

    [Fact]
    public void Batches_without_an_expiry_are_drawn_last_under_fefo()
    {
        StockItem item = EmptyItem();
        item.RegisterEntry(Quantity.Of(20m, Ml), Lot.FromCode("NO-EXPIRY"));
        item.RegisterEntry(Quantity.Of(20m, Ml), Lot.FromCode("DATED"), ExpiryDate.FromYearMonth(2027, 6));
        item.ClearDomainEvents();

        item.RegisterConsumption(Quantity.Of(20m, Ml));

        StockBatch dated = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("DATED")));
        StockBatch noExpiry = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("NO-EXPIRY")));
        Assert.True(dated.RemainingQuantity.IsZero);                    // dated drawn first
        Assert.Equal(Quantity.Of(20m, Ml), noExpiry.RemainingQuantity); // undated untouched
    }

    [Fact]
    public void Consumption_spills_across_batches_and_reports_one_allocation_per_batch()
    {
        StockItem item = EmptyItem();
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("A"), ExpiryDate.FromYearMonth(2027, 1), unitCostBrl: 2m);
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("B"), ExpiryDate.FromYearMonth(2027, 6), unitCostBrl: 3m);
        item.ClearDomainEvents();

        item.RegisterConsumption(Quantity.Of(15m, Ml));

        StockConsumedEvent consumed = Assert.IsType<StockConsumedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(2, consumed.Allocations.Count);
        // Earliest-expiring batch (A) fully drawn at its cost, then the remainder from B.
        Assert.Equal(10m, consumed.Allocations[0].Quantity.Value);
        Assert.Equal(2m, consumed.Allocations[0].UnitCostBrl);
        Assert.Equal(5m, consumed.Allocations[1].Quantity.Value);
        Assert.Equal(3m, consumed.Allocations[1].UnitCostBrl);
    }

    [Fact]
    public void A_preferred_batch_is_drawn_first_then_fefo_covers_the_remainder()
    {
        StockItem item = EmptyItem();
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("EARLY"), ExpiryDate.FromYearMonth(2027, 1));
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("PREF"), ExpiryDate.FromYearMonth(2028, 1));
        StockBatch preferred = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("PREF")));
        item.ClearDomainEvents();

        item.RegisterConsumption(Quantity.Of(5m, Ml), preferredBatchId: preferred.Id);

        // The preferred (later-expiring) batch is drawn even though FEFO would pick EARLY first.
        Assert.Equal(Quantity.Of(5m, Ml), preferred.RemainingQuantity);
        StockBatch early = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("EARLY")));
        Assert.Equal(Quantity.Of(10m, Ml), early.RemainingQuantity);
    }

    [Fact]
    public void ClassifyExpiry_uses_the_earliest_expiring_batch_with_balance()
    {
        StockItem item = EmptyItem();
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("OLD"), ExpiryDate.FromYearMonth(2020, 1));
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("NEW"), ExpiryDate.FromYearMonth(2030, 1));

        ExpiryStatus? status = item.ClassifyExpiry(FixedClock.On(2026, 7, 11), TimeSpan.FromDays(30));

        Assert.Equal(ExpiryStatus.Expired, status);
    }

    [Fact]
    public void RegisterEntry_rejects_a_negative_unit_cost()
    {
        StockItem item = NewItem(initial: 100m);

        Assert.Throws<DomainException>(() => item.RegisterEntry(Quantity.Of(10m, Ml), unitCostBrl: -1m));
    }

    [Fact]
    public void RegisterEntry_accepts_a_null_unit_cost_for_donations()
    {
        StockItem item = EmptyItem();

        item.RegisterEntry(Quantity.Of(10m, Ml), unitCostBrl: null);

        StockBatch batch = Assert.Single(item.Batches);
        Assert.Null(batch.UnitCostBrl);
    }

    [Fact]
    public void Disposal_draws_the_earliest_expiring_batch_first_to_clear_expired_stock()
    {
        StockItem item = EmptyItem();
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("EXPIRED"), ExpiryDate.FromYearMonth(2020, 1));
        item.RegisterEntry(Quantity.Of(10m, Ml), Lot.FromCode("VALID"), ExpiryDate.FromYearMonth(2030, 1));
        item.ClearDomainEvents();

        item.Dispose(Quantity.Of(10m, Ml));

        StockBatch expired = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("EXPIRED")));
        StockBatch valid = item.Batches.Single(b => Equals(b.Lot, Lot.FromCode("VALID")));
        Assert.True(expired.RemainingQuantity.IsZero);
        Assert.Equal(Quantity.Of(10m, Ml), valid.RemainingQuantity);
        StockDisposedEvent disposed = Assert.IsType<StockDisposedEvent>(Assert.Single(item.DomainEvents));
        Assert.Equal(expired.Id, Assert.Single(disposed.Allocations).BatchId);
    }
}
