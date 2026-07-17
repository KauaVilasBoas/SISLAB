using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Domain.StockItems;

/// <summary>
/// Central aggregate of the Inventory module: a stock item held by the laboratory at a single storage
/// location. It owns its <see cref="StockBatch"/> children — one per receipt, each with its own balance,
/// traceability and cost — and the balance invariants (never negative; consumption, transfer and disposal
/// never exceed the balance) and raises the domain events consumed by read models and alerts.
/// </summary>
/// <remarks>
/// <para>
/// Batch model (card [E4] #109): the on-hand <see cref="Quantity"/> is <b>derived</b> as the sum of the
/// batches' remaining balances — the aggregate keeps no separate scalar balance, so the two can never drift.
/// A receipt (<see cref="RegisterEntry"/>) creates a new batch; a consumption/disposal draws the balance down
/// <b>FEFO</b> (first-expired, first-out), spilling across batches when one is not enough. This lets the cost
/// report (card #109) value each draw at the real price of the batch it came from, and keeps FEFO aligned
/// with the expiry alerts (both read the same per-batch validity).
/// </para>
/// <para>
/// Expiry policy: an expired batch does <b>not</b> block consumption or disposal. Validity is traceability
/// data; the derived <see cref="ExpiryStatus"/> (via <see cref="ClassifyExpiry"/>) feeds alerts and the UI.
/// This reflects the LAFTE prototype, where expired stock (e.g. expired DMSO) remains listed and is disposed
/// of explicitly — decision recorded on card [E3] #21. FEFO consequently draws from expired batches first
/// (their validity is the earliest), which is the desired "use/clear the oldest first" behaviour.
/// </para>
/// <para>
/// The storage location is referenced by value (<see cref="StorageLocationId"/>); the aggregate does not know
/// the location's type. The rule "a controlled item may only reside in a controlled location" needs the
/// location type and is therefore enforced where that is known (card [E3] #23), not inside this aggregate.
/// </para>
/// <para>
/// The category is likewise referenced <b>by value</b> (<see cref="CategoryId"/>) since card [E12] #76: item
/// categories became dynamic, per-tenant rows owned by the Configuration module. That a category exists and
/// belongs to the tenant is validated in the write-side command handler via <c>ILabConfiguration</c>, not
/// inside this aggregate — exactly like <see cref="StorageLocationId"/>.
/// </para>
/// </remarks>
public sealed class StockItem : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 200;
    private const int MaxBrandLength = 120;
    private const int MaxApplicationLength = 500;

    private readonly List<StockBatch> _batches = [];

    private UnitOfMeasure _unit = default!;

    // Parameterless constructor for EF Core materialization.
    private StockItem() : base(Guid.Empty) { }

    private StockItem(
        Guid id,
        string name,
        Guid categoryId,
        string? brand,
        ContainerState containerState,
        string? application,
        bool isControlled,
        Guid storageLocationId,
        UnitOfMeasure unit,
        Quantity minimumQuantity)
        : base(id)
    {
        Name = name;
        CategoryId = categoryId;
        Brand = brand;
        ContainerState = containerState;
        Application = application;
        IsControlled = isControlled;
        StorageLocationId = storageLocationId;
        _unit = unit;
        MinimumQuantity = minimumQuantity;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    public string Name { get; private set; } = default!;

    /// <summary>Item category, referenced by value (a per-tenant Configuration category — card [E12] #76).</summary>
    public Guid CategoryId { get; private set; }

    public string? Brand { get; private set; }

    public ContainerState ContainerState { get; private set; }

    public string? Application { get; private set; }

    public bool IsControlled { get; private set; }

    /// <summary>Storage location that holds this item, referenced by value (no cross-aggregate navigation).</summary>
    public Guid StorageLocationId { get; private set; }

    /// <summary>The batches (receipts) that make up this item's stock; the balance is derived from them.</summary>
    public IReadOnlyList<StockBatch> Batches => _batches.AsReadOnly();

    /// <summary>
    /// On-hand balance, <b>derived</b> as the sum of the batches' remaining balances so it can never drift from
    /// the batch ledger. An item with no batches is zero in its fixed unit.
    /// </summary>
    public Quantity Quantity => _batches.Aggregate(
        Quantity.Zero(_unit),
        (total, batch) => total.Add(batch.RemainingQuantity));

    /// <summary>Reorder threshold; when the balance falls below it the item is considered low on stock.</summary>
    public Quantity MinimumQuantity { get; private set; } = default!;

    /// <summary>
    /// The item's fixed unit of measure, set at creation and never changed (a unit change would require
    /// converting every batch balance and would break the movement history).
    /// </summary>
    public UnitOfMeasure Unit => _unit;

    public bool IsBelowMinimum => Quantity.IsLessThan(MinimumQuantity);

    /// <summary>
    /// Registers a new stock item. The unit of the minimum threshold fixes the item's unit of measure. The
    /// initial balance, when positive, is recorded as the item's first batch (carrying its lot, expiry and
    /// optional unit cost); a zero initial quantity creates the item with no batches (stock arrives later via
    /// <see cref="RegisterEntry"/>).
    /// </summary>
    public static StockItem Register(
        string name,
        Guid categoryId,
        Guid storageLocationId,
        Quantity initialQuantity,
        Quantity minimumQuantity,
        bool isControlled = false,
        ContainerState containerState = ContainerState.Closed,
        string? brand = null,
        string? application = null,
        Lot? lot = null,
        ExpiryDate? expiry = null,
        decimal? unitCostBrl = null,
        IClock? clock = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Guard.AgainstMaxLength(name.Trim(), MaxNameLength, nameof(name));
        Guard.AgainstEmptyGuid(categoryId, nameof(categoryId));
        Guard.AgainstEmptyGuid(storageLocationId, nameof(storageLocationId));
        Guard.AgainstNull(initialQuantity, nameof(initialQuantity));
        Guard.AgainstNull(minimumQuantity, nameof(minimumQuantity));
        EnsureSameUnit(minimumQuantity, initialQuantity);

        var item = new StockItem(
            Guid.NewGuid(),
            name.Trim(),
            categoryId,
            NormalizeOptionalText(brand, MaxBrandLength, nameof(brand)),
            containerState,
            NormalizeOptionalText(application, MaxApplicationLength, nameof(application)),
            isControlled,
            storageLocationId,
            minimumQuantity.Unit,
            minimumQuantity);

        // A positive opening balance is the item's first receipt; a zero opening balance has no batch.
        if (!initialQuantity.IsZero)
            item._batches.Add(StockBatch.Receive(
                initialQuantity, lot, expiry, unitCostBrl, ResolveNow(clock), supplierPartnerId: null));

        return item;
    }

    /// <summary>Renames the item.</summary>
    public void Rename(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmed = name.Trim();
        Guard.AgainstMaxLength(trimmed, MaxNameLength, nameof(name));

        Name = trimmed;
    }

    /// <summary>
    /// Recategorises the item. The category is referenced by value (card [E12] #76); that the id is a real
    /// category of the tenant is validated in the write-side handler, not here — exactly as on creation.
    /// </summary>
    public void Recategorize(Guid categoryId)
    {
        Guard.AgainstEmptyGuid(categoryId, nameof(categoryId));
        CategoryId = categoryId;
    }

    /// <summary>
    /// Updates the descriptive metadata (brand and application); passing null or blank clears the
    /// corresponding field. This is pure identification data and does not touch the balance or traceability.
    /// </summary>
    public void Describe(string? brand, string? application)
    {
        Brand = NormalizeOptionalText(brand, MaxBrandLength, nameof(brand));
        Application = NormalizeOptionalText(application, MaxApplicationLength, nameof(application));
    }

    /// <summary>
    /// Adjusts the reorder threshold. The new minimum must share the item's unit of measure — the unit is
    /// fixed at creation and never changes here (a unit change would require converting the balance and would
    /// break the movement history). Unlike <see cref="RegisterConsumption"/>, correcting the threshold does
    /// not emit a low-stock event: this is a configuration correction, not a stock movement.
    /// </summary>
    public void AdjustMinimumQuantity(Quantity minimumQuantity)
    {
        Guard.AgainstNull(minimumQuantity, nameof(minimumQuantity));
        EnsureUnitMatchesItem(minimumQuantity);

        MinimumQuantity = minimumQuantity;
    }

    /// <summary>
    /// Relocates the item to another storage location as a metadata correction — <b>not</b> a stock
    /// transfer. Unlike <see cref="TransferTo"/>, this does not emit a <see cref="StockTransferredEvent"/>
    /// and therefore leaves no movement in the ledger: it is meant for fixing the recorded location of an
    /// item that never physically moved (card [E7] #46). The controlled/location-type invariant is enforced
    /// upstream (card #23).
    /// </summary>
    public void Relocate(Guid storageLocationId)
    {
        Guard.AgainstEmptyGuid(storageLocationId, nameof(storageLocationId));
        StorageLocationId = storageLocationId;
    }

    /// <summary>
    /// Registers an incoming stock entry, adding a new <see cref="StockBatch"/> with its own balance,
    /// traceability (lot and expiry) and optional unit cost. The optional <paramref name="occurredOn"/> and
    /// <paramref name="supplierPartnerId"/> are origin/traceability metadata carried on
    /// <see cref="StockReceivedEvent"/> so the movements read model (card [E4] #33) records when the entry
    /// happened, which supplier it came from and at what cost.
    /// </summary>
    public void RegisterEntry(
        Quantity quantity,
        Lot? lot = null,
        ExpiryDate? expiry = null,
        DateOnly? occurredOn = null,
        Guid? supplierPartnerId = null,
        decimal? unitCostBrl = null,
        IClock? clock = null)
    {
        Quantity received = EnsurePositiveOperationQuantity(quantity, "register an entry of");

        var batch = StockBatch.Receive(
            received, lot, expiry, unitCostBrl, ResolveNow(clock), supplierPartnerId);
        _batches.Add(batch);

        RaiseDomainEvent(new StockReceivedEvent(
            CompanyId, Id, batch.Id, received, Quantity, lot, expiry, unitCostBrl, occurredOn, supplierPartnerId));
    }

    /// <summary>
    /// Registers a consumption, decreasing the balance by drawing it down FEFO across the batches. Fails if
    /// the amount exceeds the balance. An expired batch is <b>not</b> blocked from being consumed (see the
    /// aggregate remarks); FEFO simply draws the earliest-expiring batches first. When
    /// <paramref name="preferredBatchId"/> is supplied that batch is drawn first (the operator picked a lot),
    /// then FEFO covers any remainder. The resulting per-batch <see cref="BatchAllocation"/> slices travel on
    /// <see cref="StockConsumedEvent"/> so the cost report (card #109) can value the consumption at the real
    /// price of each batch it came from.
    /// </summary>
    public void RegisterConsumption(
        Quantity quantity,
        DateOnly? occurredOn = null,
        Guid? experimentId = null,
        Guid? preferredBatchId = null)
    {
        Quantity consumed = EnsurePositiveOperationQuantity(quantity, "consume");
        EnsureBalanceCovers(consumed, "consume");

        bool wasBelowMinimum = IsBelowMinimum;
        IReadOnlyList<BatchAllocation> allocations = DrawDownFefo(consumed, preferredBatchId);

        RaiseDomainEvent(new StockConsumedEvent(
            CompanyId, Id, consumed, Quantity, allocations, occurredOn, experimentId));

        // Emit the low-stock signal only on the crossing (not-below → below), so the alert (E6)
        // fires once per breach instead of on every consumption while already depleted.
        if (!wasBelowMinimum && IsBelowMinimum)
            RaiseDomainEvent(new StockBelowMinimumEvent(CompanyId, Id, Quantity, MinimumQuantity));
    }

    /// <summary>
    /// Moves the whole item to another storage location. The balance is unchanged; only the location
    /// reference is updated. The controlled/location-type invariant is enforced upstream (card #23).
    /// </summary>
    public void TransferTo(Guid destinationStorageLocationId, DateOnly? occurredOn = null)
    {
        Guard.AgainstEmptyGuid(destinationStorageLocationId, nameof(destinationStorageLocationId));

        if (destinationStorageLocationId == StorageLocationId)
            throw new DomainException("Cannot transfer a stock item to its current storage location.");

        Guid origin = StorageLocationId;
        StorageLocationId = destinationStorageLocationId;

        RaiseDomainEvent(new StockTransferredEvent(
            CompanyId, Id, origin, destinationStorageLocationId, Quantity, occurredOn));
    }

    /// <summary>
    /// Discards a quantity of stock (for example an expired or unusable batch), drawing it down FEFO across the
    /// batches. Fails if the amount exceeds the balance; disposing expired stock is always allowed (FEFO draws
    /// the earliest-expiring batches first, which is exactly what a "clear the expired stock" disposal wants).
    /// </summary>
    public void Dispose(Quantity quantity, DateOnly? occurredOn = null, Guid? preferredBatchId = null)
    {
        Quantity disposed = EnsurePositiveOperationQuantity(quantity, "dispose");
        EnsureBalanceCovers(disposed, "dispose");

        IReadOnlyList<BatchAllocation> allocations = DrawDownFefo(disposed, preferredBatchId);

        RaiseDomainEvent(new StockDisposedEvent(CompanyId, Id, disposed, Quantity, allocations, occurredOn));
    }

    /// <summary>
    /// Records a physical stock count (conference) of a controlled item. This is an append-only compliance
    /// operation: it compares the counted balance with the current system balance and raises
    /// <see cref="StockCountedEvent"/> with the divergence, but <b>never changes the on-hand quantity</b>
    /// (decision recorded on card [E3] #24). Returns the divergence (counted minus system balance).
    /// </summary>
    public decimal RegisterStockCount(Quantity countedQuantity)
    {
        Guard.AgainstNull(countedQuantity, nameof(countedQuantity));
        EnsureUnitMatchesItem(countedQuantity);

        Quantity current = Quantity;
        decimal divergence = countedQuantity.Value - current.Value;

        RaiseDomainEvent(new StockCountedEvent(Id, current, countedQuantity, divergence));

        return divergence;
    }

    /// <summary>
    /// Classifies the item's validity for alerts and the UI over its <b>earliest-expiring</b> batch — the
    /// batch FEFO would draw next and the one that drives the alert. Returns <see langword="null"/> when the
    /// item has no batch carrying an expiry (n/a). Derived on demand from the batches and the supplied clock;
    /// never persisted.
    /// </summary>
    public ExpiryStatus? ClassifyExpiry(IClock clock, TimeSpan warningWindow)
    {
        ArgumentNullException.ThrowIfNull(clock);

        ExpiryDate? earliest = EarliestExpiry();
        return earliest?.GetStatus(clock, warningWindow);
    }

    /// <summary>
    /// Draws <paramref name="amount"/> down across the batches FEFO (earliest expiry first, nulls last, then
    /// oldest receipt), optionally starting with <paramref name="preferredBatchId"/>. Returns the per-batch
    /// allocations actually taken. The caller has already guaranteed the balance covers the amount, so the
    /// loop always fully satisfies it.
    /// </summary>
    private IReadOnlyList<BatchAllocation> DrawDownFefo(Quantity amount, Guid? preferredBatchId)
    {
        var allocations = new List<BatchAllocation>();
        Quantity remaining = amount;

        foreach (StockBatch batch in OrderForDrawDown(preferredBatchId))
        {
            if (remaining.IsZero)
                break;

            if (batch.IsDepleted)
                continue;

            Quantity taken = batch.DrawDown(remaining);
            if (taken.IsZero)
                continue;

            allocations.Add(new BatchAllocation(batch.Id, taken, batch.UnitCostBrl));
            remaining = remaining.Subtract(taken);
        }

        return allocations;
    }

    /// <summary>
    /// Orders the batches for a draw-down: the operator-preferred batch first (when it still has balance),
    /// then FEFO — earliest expiry first, batches with no expiry last, breaking ties by earliest receipt so
    /// the order is deterministic.
    /// </summary>
    private IEnumerable<StockBatch> OrderForDrawDown(Guid? preferredBatchId)
    {
        IEnumerable<StockBatch> fefo = _batches
            .OrderBy(batch => batch.Expiry is null)
            .ThenBy(batch => batch.Expiry?.LastValidDay ?? DateOnly.MaxValue)
            .ThenBy(batch => batch.ReceivedAtUtc);

        if (preferredBatchId is not { } preferred)
            return fefo;

        StockBatch? head = _batches.FirstOrDefault(batch => batch.Id == preferred);
        return head is null
            ? fefo
            : new[] { head }.Concat(fefo.Where(batch => batch.Id != preferred));
    }

    /// <summary>The earliest expiry among the batches that still have balance and carry a validity, or null.</summary>
    private ExpiryDate? EarliestExpiry() => _batches
        .Where(batch => !batch.IsDepleted && batch.Expiry is not null)
        .OrderBy(batch => batch.Expiry!.LastValidDay)
        .Select(batch => batch.Expiry)
        .FirstOrDefault();

    private Quantity EnsurePositiveOperationQuantity(Quantity quantity, string operation)
    {
        Guard.AgainstNull(quantity, nameof(quantity));
        EnsureUnitMatchesItem(quantity);

        if (quantity.IsZero)
            throw new DomainException($"Cannot {operation} a zero quantity.");

        return quantity;
    }

    private void EnsureBalanceCovers(Quantity amount, string operation)
    {
        Quantity current = Quantity;
        if (!current.IsGreaterThanOrEqualTo(amount))
            throw new DomainException(
                $"Cannot {operation} {amount}: only {current} is available for item '{Name}'.");
    }

    private void EnsureUnitMatchesItem(Quantity other) => EnsureSameUnit(
        Quantity.Zero(_unit), other);

    private static void EnsureSameUnit(Quantity reference, Quantity other)
    {
        if (!reference.Unit.IsCompatibleWith(other.Unit))
            throw new DomainException(
                $"Quantity unit '{other.Unit}' is incompatible with the item's unit '{reference.Unit}'.");
    }

    private static DateTime ResolveNow(IClock? clock) => clock?.UtcNow ?? DateTime.UtcNow;

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
