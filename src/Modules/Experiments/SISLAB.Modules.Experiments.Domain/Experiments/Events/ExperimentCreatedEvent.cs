using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Experiments.Events;

/// <summary>
/// Raised when a new <see cref="Experiment"/> is created. Module-internal for now (no Outbox translator in
/// this slice); carried so a future read-model projection / audit can react. <see cref="CompanyId"/> travels
/// on the event for a potential Outbox translation later.
/// </summary>
public sealed record ExperimentCreatedEvent(
    Guid CompanyId,
    Guid ExperimentId,
    ExperimentType Type,
    string Title) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
