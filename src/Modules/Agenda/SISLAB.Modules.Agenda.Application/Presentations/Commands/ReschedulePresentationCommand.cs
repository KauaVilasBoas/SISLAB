using SISLAB.Modules.Agenda.Domain.Presentations;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Presentations.Commands;

public sealed record ReschedulePresentationCommand(Guid PresentationId, DateOnly NewDate, string? Notes) : ICommand;
public sealed record CancelPresentationCommand(Guid PresentationId) : ICommand;

internal sealed class ReschedulePresentationCommandHandler : ICommandHandler<ReschedulePresentationCommand>
{
    private readonly IPresentationRepository _repository;

    public ReschedulePresentationCommandHandler(IPresentationRepository repository) => _repository = repository;

    public async Task<Unit> HandleAsync(ReschedulePresentationCommand command, CancellationToken cancellationToken = default)
    {
        Presentation? p = await _repository.GetByIdAsync(command.PresentationId, cancellationToken);
        if (p is null)
            throw new NotFoundException($"Presentation {command.PresentationId} not found.");

        p.Reschedule(command.NewDate, command.Notes);
        return Unit.Value;
    }
}

internal sealed class CancelPresentationCommandHandler : ICommandHandler<CancelPresentationCommand>
{
    private readonly IPresentationRepository _repository;

    public CancelPresentationCommandHandler(IPresentationRepository repository) => _repository = repository;

    public async Task<Unit> HandleAsync(CancelPresentationCommand command, CancellationToken cancellationToken = default)
    {
        Presentation? p = await _repository.GetByIdAsync(command.PresentationId, cancellationToken);
        if (p is null)
            throw new NotFoundException($"Presentation {command.PresentationId} not found.");

        p.Cancel();
        return Unit.Value;
    }
}
