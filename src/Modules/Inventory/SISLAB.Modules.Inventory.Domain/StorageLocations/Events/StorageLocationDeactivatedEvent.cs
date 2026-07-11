using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StorageLocations.Events;

/// <summary>
/// Raised when a <see cref="StorageLocation"/> is deactivated. A deactivated location can no longer
/// receive stock; existing balances are handled by the transfer flow (card [E3] #26).
/// </summary>
public sealed record StorageLocationDeactivatedEvent(Guid StorageLocationId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
