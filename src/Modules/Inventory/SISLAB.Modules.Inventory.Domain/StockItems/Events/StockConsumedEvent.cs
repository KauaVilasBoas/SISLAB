using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a consumption reduces the on-hand quantity of a <see cref="StockItem"/>. Crossing of the
/// minimum threshold is signalled separately by <see cref="StockBelowMinimumEvent"/>, so consumers do
/// not infer it from the resulting quantity. <see cref="CompanyId"/> is carried for the Outbox
/// translation (card [E3] #26).
/// </summary>
public sealed record StockConsumedEvent(
    Guid CompanyId,
    Guid StockItemId,
    Quantity ConsumedQuantity,
    Quantity ResultingQuantity) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
