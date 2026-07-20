using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Entries.Commands;

/// <summary>
/// Deletes a calendar entry outright (card [E10.2] #2) — for a recurring entry this removes the whole series
/// (to drop a single instance, use <see cref="CancelAgendaOccurrenceCommand"/>). Idempotent from the caller's
/// view: a missing entry surfaces as 404 rather than a silent success, so the client learns the id was stale.
/// </summary>
public sealed record DeleteAgendaEntryCommand(Guid EntryId) : ICommand;

internal sealed class DeleteAgendaEntryCommandHandler : ICommandHandler<DeleteAgendaEntryCommand>
{
    private readonly IAgendaEntryRepository _repository;

    public DeleteAgendaEntryCommandHandler(IAgendaEntryRepository repository)
        => _repository = repository;

    public async Task<Unit> HandleAsync(
        DeleteAgendaEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        AgendaEntry entry = await _repository.GetByIdAsync(command.EntryId, cancellationToken)
            ?? throw new NotFoundException($"Agenda entry {command.EntryId} not found.");

        _repository.Remove(entry);
        return Unit.Value;
    }
}
