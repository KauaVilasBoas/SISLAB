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
        decimal receivedQuantity,
        decimal resultingQuantity,
        string unit,
        string? lotCode,
        int? expiryYear,
        int? expiryMonth)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        ReceivedQuantity = receivedQuantity;
        ResultingQuantity = resultingQuantity;
        Unit = unit;
        LotCode = lotCode;
        ExpiryYear = expiryYear;
        ExpiryMonth = expiryMonth;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(StockReceivedIntegrationEvent);

    public Guid CompanyId { get; }

    public Guid StockItemId { get; }

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
}
