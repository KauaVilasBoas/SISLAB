using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Entries.Commands;

/// <summary>
/// Cancels a single occurrence of a recurring entry (card [E10.2] #2) by adding its date to the entry's
/// exclusion set (RFC 5545 <c>EXDATE</c>). The series lives on; only the one instance is suppressed.
/// </summary>
public sealed record CancelAgendaOccurrenceCommand(Guid EntryId, DateOnly OccurrenceDate) : ICommand;

internal sealed class CancelAgendaOccurrenceCommandHandler : ICommandHandler<CancelAgendaOccurrenceCommand>
{
    private readonly IAgendaEntryRepository _repository;

    public CancelAgendaOccurrenceCommandHandler(IAgendaEntryRepository repository)
        => _repository = repository;

    public async Task<Unit> HandleAsync(
        CancelAgendaOccurrenceCommand command,
        CancellationToken cancellationToken = default)
    {
        AgendaEntry entry = await _repository.GetByIdAsync(command.EntryId, cancellationToken)
            ?? throw new NotFoundException($"Agenda entry {command.EntryId} not found.");

        // A non-recurring entry has no occurrences to exclude — surface as a business rule violation (422),
        // not a raw domain exception 500.
        if (!entry.IsRecurring)
            throw new BusinessException(
                "Cannot cancel an occurrence of a non-recurring entry; delete the entry instead.");

        entry.CancelOccurrence(command.OccurrenceDate);
        return Unit.Value;
    }
}
