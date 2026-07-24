using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Collection.Events;

/// <summary>
/// Raised when a <see cref="CollectionPlan"/> is created for a batch (SISLAB-08). Module-internal for now (no Outbox
/// translator); carried so a future read-model projection or audit can react. <see cref="CompanyId"/> travels on the
/// event for a potential Outbox translation later.
/// </summary>
public sealed record CollectionPlanCreatedEvent(
    Guid CompanyId,
    Guid CollectionPlanId,
    Guid ProjectId,
    Guid BatchId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
