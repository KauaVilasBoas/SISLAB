using SISLAB.Modules.Agenda.Domain.Presentations;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Presentations.Commands;

/// <summary>Marks a presentation's reminder as sent (sets <c>ReminderSentAt</c>). Called by the reminder job after publishing the notification.</summary>
public sealed record MarkPresentationReminderSentCommand(Guid PresentationId) : ICommand;

internal sealed class MarkPresentationReminderSentCommandHandler : ICommandHandler<MarkPresentationReminderSentCommand>
{
    private readonly IPresentationRepository _repository;
    private readonly IClock _clock;

    public MarkPresentationReminderSentCommandHandler(IPresentationRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Unit> HandleAsync(MarkPresentationReminderSentCommand command, CancellationToken cancellationToken = default)
    {
        Presentation? p = await _repository.GetByIdAsync(command.PresentationId, cancellationToken);
        if (p is null)
            throw new NotFoundException($"Presentation {command.PresentationId} not found.");

        p.RecordReminderSent(_clock.UtcNow);
        return Unit.Value;
    }
}
