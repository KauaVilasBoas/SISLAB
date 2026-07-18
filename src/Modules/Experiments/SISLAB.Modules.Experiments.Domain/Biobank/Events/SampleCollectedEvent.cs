using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Biobank.Events;

/// <summary>
/// Raised when a biobank <see cref="Sample"/> is collected (card [E11] #89). Module-internal for now (no Outbox
/// translator); carried so a future read-model projection, audit or freezer-inventory sync can react.
/// <see cref="CompanyId"/> travels on the event for a potential Outbox translation later.
/// </summary>
public sealed record SampleCollectedEvent(
    Guid CompanyId,
    Guid SampleId,
    string Code,
    Guid AnimalId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
