using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.Equipments.Events;

/// <summary>Raised when a new <see cref="Equipment"/> is registered for the laboratory.</summary>
public sealed record EquipmentRegisteredEvent(
    Guid EquipmentId,
    string Name,
    string AssetTag) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
