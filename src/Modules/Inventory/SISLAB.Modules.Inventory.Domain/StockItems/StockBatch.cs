using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Inventory.Domain.StockItems;

/// <summary>
/// A physical batch (an individual receipt/lote) held under a <see cref="StockItem"/>, owning its own
/// remaining balance, traceability (lot code and expiry) and unit cost. It is a child entity of the
/// <see cref="StockItem"/> aggregate — created and mutated only through the aggregate's behaviour methods,
/// never on its own — so the aggregate stays the single guardian of the stock invariants (card [E4] #109).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a batch entity (card #109/#111).</b> The laboratory buys the same item several times, each purchase
/// with its own lot, expiry and price. Keeping the balance per batch is what lets consumption be debited
/// <b>FEFO</b> (first-expired, first-out) and lets the cost of a consumption be the real cost of the batch it
/// came from — the coordinator's "quanto se gasta por mês / por experimento" report (card #109) needs the
/// price at the granularity it was paid, which a single item-level average would blur.
/// </para>
/// <para>
/// <b>Cost (card #109).</b> <see cref="UnitCostBrl"/> is the unit price in BRL at receipt, optional because
/// donations / no-invoice items legitimately have no price (the report treats those as unpriced, never
/// distorting the total). When informed it must be non-negative. Currency is fixed to BRL in the MVP
/// (foreign currency is out of scope), so no <c>Money</c> value object is introduced — a guarded
/// <see cref="decimal"/> is the honest model.
/// </para>
/// <para>
/// <b>Expiry.</b> Reuses the aggregate's month-granularity <see cref="ExpiryDate"/> value object (the
/// laboratory records validity as month/year), so FEFO ordering and the expiry alerts share one notion of
/// validity. A batch without an expiry sorts last under FEFO (nulls last).
/// </para>
/// </remarks>
public sealed class StockBatch : Entity<Guid>
{
    // Parameterless constructor for EF Core materialization.
    private StockBatch() : base(Guid.Empty) { }

    private StockBatch(
        Guid id,
        Quantity initialQuantity,
        Lot? lot,
        ExpiryDate? expiry,
        decimal? unitCostBrl,
        DateTime receivedAtUtc,
        Guid? supplierPartnerId)
        : base(id)
    {
        InitialQuantity = initialQuantity;
        // A DISTINCT instance from InitialQuantity (same amount/unit). EF maps both as separate owned
        // entities, and one owned instance cannot be shared across two owned navigations — sharing it makes
        // EF fail to save with "owned entity without any reference to its owner".
        RemainingQuantity = Quantity.Of(initialQuantity.Value, initialQuantity.Unit);
        Lot = lot;
        Expiry = expiry;
        UnitCostBrl = unitCostBrl;
        ReceivedAtUtc = receivedAtUtc;
        SupplierPartnerId = supplierPartnerId;
    }

    /// <summary>The quantity received when the batch was created; never changes (the historical receipt size).</summary>
    public Quantity InitialQuantity { get; private set; } = default!;

    /// <summary>The balance still available in this batch; decremented by consumption and disposal, never negative.</summary>
    public Quantity RemainingQuantity { get; private set; } = default!;

    /// <summary>Lot/batch code of the receipt, or <see langword="null"/> when not lot-controlled.</summary>
    public Lot? Lot { get; private set; }

    /// <summary>Month-granularity validity of the receipt, or <see langword="null"/> when it has no expiry.</summary>
    public ExpiryDate? Expiry { get; private set; }

    /// <summary>Unit price in BRL at receipt, or <see langword="null"/> for donations / no-invoice items.</summary>
    public decimal? UnitCostBrl { get; private set; }

    /// <summary>Instant the batch was received (used as the FEFO tie-breaker when two batches share validity).</summary>
    public DateTime ReceivedAtUtc { get; private init; }

    /// <summary>Supplier the batch came from, held <b>by value</b> (Guid); no FK/navigation to the Partner aggregate.</summary>
    public Guid? SupplierPartnerId { get; private set; }

    /// <summary>True when the batch has been fully drawn down (nothing left to consume or dispose).</summary>
    public bool IsDepleted => RemainingQuantity.IsZero;

    /// <summary>
    /// Creates a new batch for a receipt. The unit of <paramref name="initialQuantity"/> must match the
    /// item's unit (guarded by the aggregate before calling). A zero or negative quantity, or a negative
    /// cost, is rejected — a receipt always adds a positive amount.
    /// </summary>
    internal static StockBatch Receive(
        Quantity initialQuantity,
        Lot? lot,
        ExpiryDate? expiry,
        decimal? unitCostBrl,
        DateTime receivedAtUtc,
        Guid? supplierPartnerId)
    {
        Guard.AgainstNull(initialQuantity, nameof(initialQuantity));

        if (initialQuantity.IsZero)
            throw new DomainException("Cannot receive a batch with a zero quantity.");

        EnsureNonNegativeCost(unitCostBrl);

        return new StockBatch(
            Guid.NewGuid(),
            initialQuantity,
            lot,
            expiry,
            unitCostBrl,
            receivedAtUtc,
            supplierPartnerId);
    }

    /// <summary>
    /// Draws <paramref name="amount"/> down from this batch's remaining balance, returning the amount actually
    /// taken — which is the smaller of <paramref name="amount"/> and the balance, so a consumption that spills
    /// across batches can take the remainder here and carry the rest to the next batch. Rejects an amount whose
    /// unit is incompatible with the batch. Called only by the aggregate's FEFO allocation.
    /// </summary>
    internal Quantity DrawDown(Quantity amount)
    {
        Guard.AgainstNull(amount, nameof(amount));

        Quantity taken = amount.IsGreaterThanOrEqualTo(RemainingQuantity) ? RemainingQuantity : amount;
        RemainingQuantity = RemainingQuantity.Subtract(taken);
        return taken;
    }

    private static void EnsureNonNegativeCost(decimal? unitCostBrl)
    {
        if (unitCostBrl is < 0m)
            throw new DomainException(
                $"Unit cost cannot be negative. Received: {unitCostBrl}.");
    }
}
