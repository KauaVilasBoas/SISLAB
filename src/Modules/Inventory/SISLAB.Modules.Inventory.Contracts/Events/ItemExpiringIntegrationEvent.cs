using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Contracts.Events;

/// <summary>
/// Public, flattened contract signalling that an item is approaching (or has reached) its expiry.
/// Unlike the stock-movement events, expiry is <em>not</em> raised by the aggregate: it is a derived,
/// time-based condition with no state change to hang a domain event on. The scheduled job (E6) scans
/// balances against the clock and publishes this event, whose contract is owned here (card [E3] #26)
/// so both the emitter and consumers share one definition.
/// </summary>
public sealed record ItemExpiringIntegrationEvent : IIntegrationEvent
{
    public ItemExpiringIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        Guid stockItemId,
        int expiryYear,
        int expiryMonth,
        DateOnly lastValidDay,
        bool isExpired)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        StockItemId = stockItemId;
        ExpiryYear = expiryYear;
        ExpiryMonth = expiryMonth;
        LastValidDay = lastValidDay;
        IsExpired = isExpired;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(ItemExpiringIntegrationEvent);

    public Guid CompanyId { get; }

    public Guid StockItemId { get; }

    public int ExpiryYear { get; }

    /// <summary>Expiry month, 1-12.</summary>
    public int ExpiryMonth { get; }

    /// <summary>Last calendar day the item is still valid (inclusive).</summary>
    public DateOnly LastValidDay { get; }

    /// <summary>True when the scan found the item already past its last valid day.</summary>
    public bool IsExpired { get; }
}
