using SISLAB.Modules.Agenda.Domain.Bioterium;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Bioterium.Commands;

public sealed record MarkBioteriumDoneCommand(Guid AssignmentId, string? Notes) : ICommand;

internal sealed class MarkBioteriumDoneCommandHandler : ICommandHandler<MarkBioteriumDoneCommand>
{
    private readonly IBioteriumRepository _repository;

    public MarkBioteriumDoneCommandHandler(IBioteriumRepository repository) => _repository = repository;

    public async Task<Unit> HandleAsync(MarkBioteriumDoneCommand command, CancellationToken cancellationToken = default)
    {
        BioteriumAssignment? assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        if (assignment is null)
            throw new NotFoundException($"Bioterium assignment {command.AssignmentId} not found.");

        assignment.MarkDone(command.Notes);
        return Unit.Value;
    }
}
