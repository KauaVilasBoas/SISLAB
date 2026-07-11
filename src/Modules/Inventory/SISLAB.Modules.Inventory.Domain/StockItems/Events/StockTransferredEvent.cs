using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a <see cref="StockItem"/> is moved from one storage location to another. Locations are
/// referenced by value (their identifiers); the aggregate does not know their type. The invariant that
/// a controlled item may only reside in a controlled location is enforced where the location type is
/// known (card [E3] #23), not inside this aggregate.
/// </summary>
public sealed record StockTransferredEvent(
    Guid StockItemId,
    Guid FromStorageLocationId,
    Guid ToStorageLocationId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
