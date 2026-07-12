using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.Partners.Events;

/// <summary>Raised when a new <see cref="Partner"/> is registered for the laboratory.</summary>
public sealed record PartnerRegisteredEvent(
    Guid PartnerId,
    string Name,
    PartnerType Type) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
