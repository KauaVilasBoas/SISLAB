using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Contracts.Events;

/// <summary>
/// Public, flattened contract published when a quantity of an item is discarded (for example an expired
/// or unusable batch), reducing the on-hand quantity. Consumed via the Outbox by the movements read model
/// (card [E7] #47), which appends one ledger row per disposal. The resulting quantity lets consumers
/// reconcile balances without reloading the aggregate.
/// </summary>
public sealed record StockDisposedIntegrationEvent : IIntegrationEvent
{
    public StockDisposedIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        Guid stockItemId,
        decimal disposedQuantity,
        decimal resultingQuantity,
        string unit,
        IReadOnlyList<StockBatchAllocationDto> allocations,
        DateOnly? occurredOn = null)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        DisposedQuantity = disposedQuantity;
        ResultingQuantity = resultingQuantity;
        Unit = unit;
        Allocations = allocations;
        OccurredOn = occurredOn;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(StockDisposedIntegrationEvent);

    public Guid CompanyId { get; }

    public Guid StockItemId { get; }

    public decimal DisposedQuantity { get; }

    public decimal ResultingQuantity { get; }

    /// <summary>Symbol of the item's unit of measure (e.g. "mL", "g").</summary>
    public string Unit { get; }

    /// <summary>
    /// The per-batch slices this disposal was drawn from under FEFO (card [E4] #109), each with the batch it
    /// came from and its unit cost — so the read model projects one ledger row per slice.
    /// </summary>
    public IReadOnlyList<StockBatchAllocationDto> Allocations { get; }

    /// <summary>
    /// Business date the disposal occurred on (operator-supplied), or <see langword="null"/> when not
    /// informed — consumers fall back to <see cref="OccurredOnUtc"/>. Origin/traceability metadata for the
    /// movements read model (card [E7] #47).
    /// </summary>
    public DateOnly? OccurredOn { get; }
}
