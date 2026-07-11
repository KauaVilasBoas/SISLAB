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
        string unit)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        ConsumedQuantity = consumedQuantity;
        ResultingQuantity = resultingQuantity;
        Unit = unit;
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
}
