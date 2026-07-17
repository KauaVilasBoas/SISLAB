using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Contracts.Events;

/// <summary>
/// Public, flattened contract published when a stock entry increases the on-hand quantity of an item.
/// Consumed via the Outbox by other bounded contexts and read models (e.g. E4 stock ledger, E6 alerts).
///
/// Flattened by design: the internal <c>Quantity</c>/<c>Lot</c>/<c>ExpiryDate</c> value objects are
/// decomposed into primitives so consumers never depend on the Inventory domain. <see cref="CompanyId"/>
/// travels with the event so cross-tenant consumers can scope their reaction.
/// </summary>
public sealed record StockReceivedIntegrationEvent : IIntegrationEvent
{
    public StockReceivedIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        Guid stockItemId,
        Guid stockBatchId,
        decimal receivedQuantity,
        decimal resultingQuantity,
        string unit,
        string? lotCode,
        int? expiryYear,
        int? expiryMonth,
        decimal? unitCostBrl = null,
        DateOnly? occurredOn = null,
        Guid? supplierPartnerId = null)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        StockBatchId = stockBatchId;
        ReceivedQuantity = receivedQuantity;
        ResultingQuantity = resultingQuantity;
        Unit = unit;
        LotCode = lotCode;
        ExpiryYear = expiryYear;
        ExpiryMonth = expiryMonth;
        UnitCostBrl = unitCostBrl;
        OccurredOn = occurredOn;
        SupplierPartnerId = supplierPartnerId;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(StockReceivedIntegrationEvent);

    public Guid CompanyId { get; }

    public Guid StockItemId { get; }

    /// <summary>The batch (receipt) this entry created (card [E4] #109); the ledger row points at it.</summary>
    public Guid StockBatchId { get; }

    public decimal ReceivedQuantity { get; }

    public decimal ResultingQuantity { get; }

    /// <summary>Symbol of the item's unit of measure (e.g. "mL", "g").</summary>
    public string Unit { get; }

    /// <summary>Lot/batch code of the received quantity, or <see langword="null"/> when not lot-controlled.</summary>
    public string? LotCode { get; }

    /// <summary>Expiry year of the received quantity, or <see langword="null"/> when it has no expiry.</summary>
    public int? ExpiryYear { get; }

    /// <summary>Expiry month (1-12) of the received quantity, or <see langword="null"/> when it has no expiry.</summary>
    public int? ExpiryMonth { get; }

    /// <summary>Unit cost in BRL of the received batch (card [E4] #109), or <see langword="null"/> for donations.</summary>
    public decimal? UnitCostBrl { get; }

    /// <summary>
    /// Business date the entry occurred on (operator-supplied), or <see langword="null"/> when not
    /// informed — consumers fall back to <see cref="OccurredOnUtc"/>. Origin/traceability metadata for
    /// the movements read model (card [E4] #33).
    /// </summary>
    public DateOnly? OccurredOn { get; }

    /// <summary>
    /// Supplier partner the entry came from, held <b>by value</b> (Guid), or <see langword="null"/> when
    /// not informed. No FK/navigation to the Partner aggregate — a cross-reference for reporting only.
    /// </summary>
    public Guid? SupplierPartnerId { get; }
}
