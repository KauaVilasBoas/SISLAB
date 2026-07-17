using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Projects.Events;

/// <summary>
/// Raised when a <see cref="Batch"/> is started and its design is frozen (card [E11] #73). Module-internal;
/// carries the frozen <see cref="DesignVersion"/> so a projection can record the reproducible cohort snapshot.
/// The <see cref="ProjectId"/> / <see cref="BatchId"/> cross any future boundary <b>by value</b>.
/// </summary>
public sealed record BatchStartedEvent(
    Guid CompanyId,
    Guid ProjectId,
    Guid BatchId,
    int DesignVersion) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
