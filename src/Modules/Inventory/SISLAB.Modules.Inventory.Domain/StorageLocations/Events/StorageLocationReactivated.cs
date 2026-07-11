using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.StorageLocations.Events;

/// <summary>Raised when a previously deactivated <see cref="StorageLocation"/> is put back in service.</summary>
public sealed record StorageLocationReactivated(Guid StorageLocationId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
