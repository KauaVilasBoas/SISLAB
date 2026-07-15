using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Contracts.Events;

/// <summary>
/// Public, flattened contract published when an item is moved from one storage location to another.
/// Consumed via the Outbox by the movements read model (card [E7] #47), which appends one ledger row per
/// transfer. A transfer moves the whole balance and changes no quantity, so <see cref="MovedQuantity"/> is
/// the on-hand balance that crossed — carried purely for traceability, not a balance delta.
/// </summary>
public sealed record StockTransferredIntegrationEvent : IIntegrationEvent
{
    public StockTransferredIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        Guid stockItemId,
        Guid fromStorageLocationId,
        Guid toStorageLocationId,
        decimal movedQuantity,
        string unit,
        DateOnly? occurredOn = null)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        FromStorageLocationId = fromStorageLocationId;
        ToStorageLocationId = toStorageLocationId;
        MovedQuantity = movedQuantity;
        Unit = unit;
        OccurredOn = occurredOn;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(StockTransferredIntegrationEvent);

    public Guid CompanyId { get; }

    public Guid StockItemId { get; }

    /// <summary>Origin storage location, referenced by value (no navigation).</summary>
    public Guid FromStorageLocationId { get; }

    /// <summary>Destination storage location, referenced by value (no navigation).</summary>
    public Guid ToStorageLocationId { get; }

    /// <summary>On-hand balance that crossed to the destination (traceability, not a delta).</summary>
    public decimal MovedQuantity { get; }

    /// <summary>Symbol of the item's unit of measure (e.g. "mL", "g").</summary>
    public string Unit { get; }

    /// <summary>
    /// Business date the transfer occurred on (operator-supplied), or <see langword="null"/> when not
    /// informed — consumers fall back to <see cref="OccurredOnUtc"/>. Origin/traceability metadata for the
    /// movements read model (card [E7] #47).
    /// </summary>
    public DateOnly? OccurredOn { get; }
}
