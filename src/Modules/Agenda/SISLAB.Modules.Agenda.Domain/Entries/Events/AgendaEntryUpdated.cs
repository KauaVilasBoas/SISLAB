using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Agenda.Domain.Entries.Events;

/// <summary>
/// Raised when an existing <see cref="AgendaEntry"/> has its scheduling or descriptive fields changed
/// (card [E10.1] #1). A "this and following" / "only this" split raises this on the truncated original entry
/// and an <see cref="AgendaEntryCreated"/> on the new one; an "all occurrences" edit raises this alone.
/// </summary>
public sealed record AgendaEntryUpdated(
    Guid CompanyId,
    Guid EntryId,
    DateTime StartDateUtc) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
