using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Agenda.Domain.Entries.Events;

/// <summary>
/// Raised when a single occurrence of a recurring <see cref="AgendaEntry"/> is cancelled (card [E10.1] #1) by
/// adding its date to the entry's exclusion set (RFC 5545 <c>EXDATE</c> semantics). The series itself lives on;
/// only the <see cref="OccurrenceDate"/> instance is suppressed.
/// </summary>
public sealed record AgendaEntryOccurrenceCancelled(
    Guid CompanyId,
    Guid EntryId,
    DateOnly OccurrenceDate) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
