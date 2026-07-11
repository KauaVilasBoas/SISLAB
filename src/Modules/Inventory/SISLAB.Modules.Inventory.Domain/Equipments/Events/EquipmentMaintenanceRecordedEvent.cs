using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.Equipments.Events;

/// <summary>Raised when a maintenance event is logged against an <see cref="Equipment"/>.</summary>
public sealed record EquipmentMaintenanceRecordedEvent(
    Guid EquipmentId,
    DateOnly Date,
    MaintenanceType Type) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
