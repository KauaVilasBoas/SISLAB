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
        StockItemCategory category,
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
        Category = category;
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

    public StockItemCategory Category { get; private set; }

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
        StockItemCategory category,
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
        Guard.AgainstEmptyGuid(storageLocationId, nameof(storageLocationId));
        Guard.AgainstNull(initialQuantity, nameof(initialQuantity));
        Guard.AgainstNull(minimumQuantity, nameof(minimumQuantity));
        EnsureSameUnit(initialQuantity, minimumQuantity);

        return new StockItem(
            Guid.NewGuid(),
            name.Trim(),
            category,
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

    /// <summary>
    /// Registers an incoming stock entry, increasing the balance and updating the traceability data
    /// (lot and expiry) of the newly received batch.
    /// </summary>
    public void RegisterEntry(Quantity quantity, Lot? lot = null, ExpiryDate? expiry = null)
    {
        Quantity received = EnsurePositiveOperationQuantity(quantity, "register an entry of");

        _quantity = _quantity.Add(received);
        Lot = lot ?? Lot;
        Expiry = expiry ?? Expiry;

        RaiseDomainEvent(new StockReceivedEvent(CompanyId, Id, received, _quantity, Lot, Expiry));
    }

    /// <summary>
    /// Registers a consumption, decreasing the balance. Fails if the amount exceeds the balance. An
    /// expired item is <b>not</b> blocked from being consumed (see the aggregate remarks).
    /// </summary>
    public void RegisterConsumption(Quantity quantity)
    {
        Quantity consumed = EnsurePositiveOperationQuantity(quantity, "consume");
        EnsureBalanceCovers(consumed, "consume");

        bool wasBelowMinimum = IsBelowMinimum;
        _quantity = _quantity.Subtract(consumed);

        RaiseDomainEvent(new StockConsumedEvent(CompanyId, Id, consumed, _quantity));

        // Emit the low-stock signal only on the crossing (not-below → below), so the alert (E6)
        // fires once per breach instead of on every consumption while already depleted.
        if (!wasBelowMinimum && IsBelowMinimum)
            RaiseDomainEvent(new StockBelowMinimumEvent(CompanyId, Id, _quantity, MinimumQuantity));
    }

    /// <summary>
    /// Moves the whole item to another storage location. The balance is unchanged; only the location
    /// reference is updated. The controlled/location-type invariant is enforced upstream (card #23).
    /// </summary>
    public void TransferTo(Guid destinationStorageLocationId)
    {
        Guard.AgainstEmptyGuid(destinationStorageLocationId, nameof(destinationStorageLocationId));

        if (destinationStorageLocationId == StorageLocationId)
            throw new DomainException("Cannot transfer a stock item to its current storage location.");

        Guid origin = StorageLocationId;
        StorageLocationId = destinationStorageLocationId;

        RaiseDomainEvent(new StockTransferredEvent(Id, origin, destinationStorageLocationId));
    }

    /// <summary>
    /// Discards a quantity of stock (for example an expired or unusable batch), decreasing the balance.
    /// Fails if the amount exceeds the balance; disposing expired stock is always allowed.
    /// </summary>
    public void Dispose(Quantity quantity)
    {
        Quantity disposed = EnsurePositiveOperationQuantity(quantity, "dispose");
        EnsureBalanceCovers(disposed, "dispose");

        _quantity = _quantity.Subtract(disposed);

        RaiseDomainEvent(new StockDisposedEvent(Id, disposed, _quantity));
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
