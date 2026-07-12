using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.Equipments.Events;

/// <summary>Raised when an <see cref="Equipment"/> moves between operational statuses.</summary>
public sealed record EquipmentStatusChangedEvent(
    Guid EquipmentId,
    EquipmentStatus PreviousStatus,
    EquipmentStatus NewStatus) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
