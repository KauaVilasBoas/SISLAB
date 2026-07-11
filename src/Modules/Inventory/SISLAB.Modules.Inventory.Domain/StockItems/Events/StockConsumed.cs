using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a consumption reduces the on-hand quantity of a <see cref="StockItem"/>. The resulting
/// quantity lets consumers detect a crossing of the minimum threshold; the dedicated
/// <c>StockBelowThreshold</c> event and its Outbox handling are owned by card [E3] #26.
/// </summary>
public sealed record StockConsumed(
    Guid StockItemId,
    Quantity ConsumedQuantity,
    Quantity ResultingQuantity) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
