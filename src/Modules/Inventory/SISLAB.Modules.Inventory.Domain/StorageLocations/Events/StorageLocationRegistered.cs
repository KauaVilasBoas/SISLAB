using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StorageLocations.Events;

/// <summary>Raised when a new <see cref="StorageLocation"/> is registered for the laboratory.</summary>
public sealed record StorageLocationRegistered(
    Guid StorageLocationId,
    string Name,
    StorageLocationType Type) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
