using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Inventory.Domain.Partners.Events;

/// <summary>
/// Raised when a <see cref="Partner"/> is deactivated. A deactivated partner is kept for the traceability
/// of past stock entries but can no longer be selected as the origin of a new one.
/// </summary>
public sealed record PartnerDeactivatedEvent(Guid PartnerId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
