using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Contracts.Events;

/// <summary>
/// Public, flattened contract published when a consumption reduces the on-hand quantity of an item.
/// Consumed via the Outbox by read models (E4) and alert projections (E6). The resulting quantity lets
/// consumers reconcile balances without reloading the aggregate. Crossing of the minimum threshold is
/// signalled by the dedicated <see cref="StockBelowMinimumIntegrationEvent"/>, not inferred from here.
/// </summary>
public sealed record StockConsumedIntegrationEvent : IIntegrationEvent
{
    public StockConsumedIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        Guid stockItemId,
        decimal consumedQuantity,
        decimal resultingQuantity,
        string unit,
        IReadOnlyList<StockBatchAllocationDto> allocations,
        DateOnly? occurredOn = null,
        Guid? experimentId = null)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        ConsumedQuantity = consumedQuantity;
        ResultingQuantity = resultingQuantity;
        Unit = unit;
        Allocations = allocations;
        OccurredOn = occurredOn;
        ExperimentId = experimentId;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(StockConsumedIntegrationEvent);

    public Guid CompanyId { get; }

    public Guid StockItemId { get; }

    public decimal ConsumedQuantity { get; }

    public decimal ResultingQuantity { get; }

    /// <summary>Symbol of the item's unit of measure (e.g. "mL", "g").</summary>
    public string Unit { get; }

    /// <summary>
    /// The per-batch slices this consumption was drawn from under FEFO (card [E4] #109), each with the batch
    /// it came from and its unit cost — so the read model projects one costed ledger row per slice.
    /// </summary>
    public IReadOnlyList<StockBatchAllocationDto> Allocations { get; }

    /// <summary>
    /// Business date the consumption occurred on (operator-supplied), or <see langword="null"/> when not
    /// informed — consumers fall back to <see cref="OccurredOnUtc"/>. Origin/traceability metadata for the
    /// movements read model (card [E4] #33) and the consumption report (card #31).
    /// </summary>
    public DateOnly? OccurredOn { get; }

    /// <summary>
    /// Experiment the consumption fed, held <b>by value</b> (Guid), or <see langword="null"/> when not
    /// informed. No FK/navigation to the Experiment module — a cross-reference for reporting only.
    /// </summary>
    public Guid? ExperimentId { get; }
}
