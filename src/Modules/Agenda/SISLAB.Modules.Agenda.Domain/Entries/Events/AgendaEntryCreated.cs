using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Agenda.Domain.Entries.Events;

/// <summary>
/// Raised when a new <see cref="AgendaEntry"/> is created (card [E10.1] #1). Module-internal for now; carried
/// so a future read-model projection, audit trail or reminder-scheduling side effect can react. The
/// <see cref="CompanyId"/> travels on the event for a potential Outbox translation later.
/// </summary>
public sealed record AgendaEntryCreated(
    Guid CompanyId,
    Guid EntryId,
    AgendaActivityType ActivityType,
    DateTime StartDateUtc,
    bool IsRecurring) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
