using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a stock entry increases the on-hand quantity of a <see cref="StockItem"/>. Carries the
/// received amount and the resulting balance so downstream consumers (read models, integrations) can
/// react without reloading the aggregate. The public integration contract and Outbox translation are
/// owned by card [E3] #26; this internal event only records that the entry happened.
/// </summary>
public sealed record StockReceived(
    Guid StockItemId,
    Quantity ReceivedQuantity,
    Quantity ResultingQuantity,
    Lot? Lot,
    ExpiryDate? Expiry) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
