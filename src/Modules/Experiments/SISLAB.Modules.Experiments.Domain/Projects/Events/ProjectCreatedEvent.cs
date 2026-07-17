using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Projects.Events;

/// <summary>
/// Raised when a new in vivo <see cref="Project"/> is created (card [E11] #73). Module-internal for now (no
/// Outbox translator); carried so a future read-model projection or audit can react. <see cref="CompanyId"/>
/// travels on the event for a potential Outbox translation later.
/// </summary>
public sealed record ProjectCreatedEvent(
    Guid CompanyId,
    Guid ProjectId,
    string Name) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
