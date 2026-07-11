using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.Partners.Events;

/// <summary>Raised when a deactivated <see cref="Partner"/> is put back in service.</summary>
public sealed record PartnerReactivatedEvent(Guid PartnerId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
