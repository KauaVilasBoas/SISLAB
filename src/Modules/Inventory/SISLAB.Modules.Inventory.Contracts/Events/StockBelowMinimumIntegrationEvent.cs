using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Contracts.Events;

/// <summary>
/// Public, flattened contract published when a consumption drives an item's balance below its reorder
/// threshold — i.e. the moment the balance <em>crosses</em> the minimum. It is the trigger for the
/// low-stock alert (E6): the alert job reacts to this event rather than polling balances. Emitted once
/// per crossing (not on every consumption while already below), so consumers are not flooded.
/// </summary>
public sealed record StockBelowMinimumIntegrationEvent : IIntegrationEvent
{
    public StockBelowMinimumIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        Guid stockItemId,
        decimal currentQuantity,
        decimal minimumQuantity,
        string unit)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        CurrentQuantity = currentQuantity;
        MinimumQuantity = minimumQuantity;
        Unit = unit;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(StockBelowMinimumIntegrationEvent);

    public Guid CompanyId { get; }

    public Guid StockItemId { get; }

    /// <summary>On-hand quantity after the consumption that crossed the threshold.</summary>
    public decimal CurrentQuantity { get; }

    /// <summary>Reorder threshold the balance fell below.</summary>
    public decimal MinimumQuantity { get; }

    /// <summary>Symbol of the item's unit of measure (e.g. "mL", "g").</summary>
    public string Unit { get; }
}
