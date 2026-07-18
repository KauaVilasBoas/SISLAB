using SISLAB.Modules.Agenda.Domain.Bioterium;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Bioterium.Commands;

public sealed record SwapBioteriumCommand(
    Guid AssignmentId,
    string NewResponsibleName,
    string? Reason) : ICommand;

internal sealed class SwapBioteriumCommandHandler : ICommandHandler<SwapBioteriumCommand>
{
    private readonly IBioteriumRepository _repository;

    public SwapBioteriumCommandHandler(IBioteriumRepository repository) => _repository = repository;

    public async Task<Unit> HandleAsync(SwapBioteriumCommand command, CancellationToken cancellationToken = default)
    {
        BioteriumAssignment? assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        if (assignment is null)
            throw new NotFoundException($"Bioterium assignment {command.AssignmentId} not found.");

        assignment.Swap(command.NewResponsibleName, command.Reason);
        return Unit.Value;
    }
}
