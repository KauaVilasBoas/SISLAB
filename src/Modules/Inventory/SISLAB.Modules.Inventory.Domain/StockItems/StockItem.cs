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
/// location. It owns the on-hand quantity and the balance invariants (never negative; consumption,
/// transfer and disposal never exceed the balance) and raises the domain events consumed by read
/// models and alerts.
/// </summary>
/// <remarks>
/// <para>
/// Expiry policy: an expired item does <b>not</b> block consumption or disposal. Validity is
/// traceability data; the derived <see cref="ExpiryStatus"/> (via <see cref="ClassifyExpiry"/>) feeds
/// alerts and the UI. This reflects the LAFTE prototype, where expired stock (e.g. expired DMSO)
/// remains listed and is disposed of explicitly — decision recorded on card [E3] #21.
/// </para>
/// <para>
/// The storage location is referenced by value (<see cref="StorageLocationId"/>); the aggregate does
/// not know the location's type. The rule "a controlled item may only reside in a controlled
/// location" needs the location type and is therefore enforced where that is known (card [E3] #23),
/// not inside this aggregate.
/// </para>
/// <para>
/// The category is likewise referenced <b>by value</b> (<see cref="CategoryId"/>) since card [E12] #76:
/// item categories became dynamic, per-tenant rows owned by the Configuration module, replacing the old
/// closed <c>StockItemCategory</c> enum. The aggregate does not know the category's name or its controlled
/// flag; that a category exists and belongs to the tenant is validated in the write-side command handler
/// via <c>ILabConfiguration</c> (same pattern as the supplier guard), not inside this aggregate — exactly
/// like <see cref="StorageLocationId"/>.
/// </para>
/// </remarks>
public sealed class StockItem : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 200;
    private const int MaxBrandLength = 120;
    private const int MaxApplicationLength = 500;

    private Quantity _quantity = default!;

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
        Quantity quantity,
        Quantity minimumQuantity,
        Lot? lot,
        ExpiryDate? expiry)
        : base(id)
    {
        Name = name;
        CategoryId = categoryId;
        Brand = brand;
        ContainerState = containerState;
        Application = application;
        IsControlled = isControlled;
        StorageLocationId = storageLocationId;
        _quantity = quantity;
        MinimumQuantity = minimumQuantity;
        Lot = lot;
        Expiry = expiry;
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

    public Quantity Quantity => _quantity;

    /// <summary>Reorder threshold; when the balance falls below it the item is considered low on stock.</summary>
    public Quantity MinimumQuantity { get; private set; } = default!;

    public Lot? Lot { get; private set; }

    public ExpiryDate? Expiry { get; private set; }

    public bool IsBelowMinimum => Quantity.IsLessThan(MinimumQuantity);

    /// <summary>
    /// Registers a new stock item with an initial balance. The unit of the initial quantity fixes the
    /// item's unit of measure; the minimum threshold must share that unit.
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
        ExpiryDate? expiry = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Guard.AgainstMaxLength(name.Trim(), MaxNameLength, nameof(name));
        Guard.AgainstEmptyGuid(categoryId, nameof(categoryId));
        Guard.AgainstEmptyGuid(storageLocationId, nameof(storageLocationId));
        Guard.AgainstNull(initialQuantity, nameof(initialQuantity));
        Guard.AgainstNull(minimumQuantity, nameof(minimumQuantity));
        EnsureSameUnit(initialQuantity, minimumQuantity);

        return new StockItem(
            Guid.NewGuid(),
            name.Trim(),
            categoryId,
            NormalizeOptionalText(brand, MaxBrandLength, nameof(brand)),
            containerState,
            NormalizeOptionalText(application, MaxApplicationLength, nameof(application)),
            isControlled,
            storageLocationId,
            initialQuantity,
            minimumQuantity,
            lot,
            expiry);
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
        EnsureSameUnit(_quantity, minimumQuantity);

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
    /// Registers an incoming stock entry, increasing the balance and updating the traceability data
    /// (lot and expiry) of the newly received batch. The optional <paramref name="occurredOn"/> and
    /// <paramref name="supplierPartnerId"/> are origin/traceability metadata: they are not folded into
    /// the aggregate state, only carried on <see cref="StockReceivedEvent"/> so the movements read model
    /// (card [E4] #33) can record when the entry happened and which supplier it came from.
    /// </summary>
    public void RegisterEntry(
        Quantity quantity,
        Lot? lot = null,
        ExpiryDate? expiry = null,
        DateOnly? occurredOn = null,
        Guid? supplierPartnerId = null)
    {
        Quantity received = EnsurePositiveOperationQuantity(quantity, "register an entry of");

        _quantity = _quantity.Add(received);
        Lot = lot ?? Lot;
        Expiry = expiry ?? Expiry;

        RaiseDomainEvent(new StockReceivedEvent(
            CompanyId, Id, received, _quantity, Lot, Expiry, occurredOn, supplierPartnerId));
    }

    /// <summary>
    /// Registers a consumption, decreasing the balance. Fails if the amount exceeds the balance. An
    /// expired item is <b>not</b> blocked from being consumed (see the aggregate remarks). The optional
    /// <paramref name="occurredOn"/> and <paramref name="experimentId"/> are origin/traceability
    /// metadata: they are not folded into the aggregate state, only carried on
    /// <see cref="StockConsumedEvent"/> so the movements read model (card [E4] #33) and the consumption
    /// report (card #31) can record when the consumption happened and which experiment it fed.
    /// </summary>
    public void RegisterConsumption(
        Quantity quantity,
        DateOnly? occurredOn = null,
        Guid? experimentId = null)
    {
        Quantity consumed = EnsurePositiveOperationQuantity(quantity, "consume");
        EnsureBalanceCovers(consumed, "consume");

        bool wasBelowMinimum = IsBelowMinimum;
        _quantity = _quantity.Subtract(consumed);

        RaiseDomainEvent(new StockConsumedEvent(CompanyId, Id, consumed, _quantity, occurredOn, experimentId));

        // Emit the low-stock signal only on the crossing (not-below → below), so the alert (E6)
        // fires once per breach instead of on every consumption while already depleted.
        if (!wasBelowMinimum && IsBelowMinimum)
            RaiseDomainEvent(new StockBelowMinimumEvent(CompanyId, Id, _quantity, MinimumQuantity));
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
            CompanyId, Id, origin, destinationStorageLocationId, _quantity, occurredOn));
    }

    /// <summary>
    /// Discards a quantity of stock (for example an expired or unusable batch), decreasing the balance.
    /// Fails if the amount exceeds the balance; disposing expired stock is always allowed.
    /// </summary>
    public void Dispose(Quantity quantity, DateOnly? occurredOn = null)
    {
        Quantity disposed = EnsurePositiveOperationQuantity(quantity, "dispose");
        EnsureBalanceCovers(disposed, "dispose");

        _quantity = _quantity.Subtract(disposed);

        RaiseDomainEvent(new StockDisposedEvent(CompanyId, Id, disposed, _quantity, occurredOn));
    }

    /// <summary>
    /// Records a physical stock count (conference) of a controlled item. This is an append-only
    /// compliance operation: it compares the counted balance with the current system balance and
    /// raises <see cref="StockCountedEvent"/> with the divergence, but <b>never changes the on-hand
    /// quantity</b> (decision recorded on card [E3] #24). Corrections, when needed, follow the normal
    /// entry or disposal flow. Returns the divergence (counted minus system balance) so the caller can
    /// surface it; a zero divergence still produces a record, because "counted and matched" is itself a
    /// compliance fact worth keeping.
    /// </summary>
    public decimal RegisterStockCount(Quantity countedQuantity)
    {
        Guard.AgainstNull(countedQuantity, nameof(countedQuantity));
        EnsureSameUnit(_quantity, countedQuantity);

        decimal divergence = countedQuantity.Value - _quantity.Value;

        RaiseDomainEvent(new StockCountedEvent(Id, _quantity, countedQuantity, divergence));

        return divergence;
    }

    /// <summary>
    /// Classifies the item's validity for alerts and the UI. Returns <see langword="null"/> for items
    /// without an expiry date (n/a). This is derived on demand from <see cref="Expiry"/> and the
    /// supplied clock; it is never persisted.
    /// </summary>
    public ExpiryStatus? ClassifyExpiry(IClock clock, TimeSpan warningWindow)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return Expiry?.GetStatus(clock, warningWindow);
    }

    private Quantity EnsurePositiveOperationQuantity(Quantity quantity, string operation)
    {
        Guard.AgainstNull(quantity, nameof(quantity));
        EnsureSameUnit(_quantity, quantity);

        if (quantity.IsZero)
            throw new DomainException($"Cannot {operation} a zero quantity.");

        return quantity;
    }

    private void EnsureBalanceCovers(Quantity amount, string operation)
    {
        if (!_quantity.IsGreaterThanOrEqualTo(amount))
            throw new DomainException(
                $"Cannot {operation} {amount}: only {_quantity} is available for item '{Name}'.");
    }

    private static void EnsureSameUnit(Quantity reference, Quantity other)
    {
        if (!reference.Unit.IsCompatibleWith(other.Unit))
            throw new DomainException(
                $"Quantity unit '{other.Unit}' is incompatible with the item's unit '{reference.Unit}'.");
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
