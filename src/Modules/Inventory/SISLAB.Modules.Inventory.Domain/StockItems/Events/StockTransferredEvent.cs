using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StockItems.Events;

/// <summary>
/// Raised when a <see cref="StockItem"/> is moved from one storage location to another. Locations are
/// referenced by value (their identifiers); the aggregate does not know their type. The invariant that
/// a controlled item may only reside in a controlled location is enforced where the location type is
/// known (card [E3] #23), not inside this aggregate.
/// </summary>
/// <remarks>
/// A transfer moves the whole balance, so it changes no quantity; <see cref="MovedQuantity"/> is the item's
/// on-hand balance at the moment of the move, carried purely so the movements ledger (card [E7] #47) can
/// show how much crossed and in which unit. <see cref="CompanyId"/> is carried for the Outbox translation
/// (card [E3] #26), and <see cref="OccurredOn"/> is the operator-supplied business date (falling back to the
/// emission instant when omitted). None of these are domain invariants.
/// </remarks>
public sealed record StockTransferredEvent(
    Guid CompanyId,
    Guid StockItemId,
    Guid FromStorageLocationId,
    Guid ToStorageLocationId,
    Quantity MovedQuantity,
    DateOnly? OccurredOn = null) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
