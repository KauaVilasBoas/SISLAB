using SISLAB.Modules.Agenda.Domain.Presentations;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Presentations.Commands;

public sealed record SchedulePresentationCommand(
    PresentationType Type,
    string Title,
    string? Doi,
    string PresenterName,
    DateOnly ScheduledDate,
    string? Notes) : ICommand<Guid>;

internal sealed class SchedulePresentationCommandHandler : ICommandHandler<SchedulePresentationCommand, Guid>
{
    private readonly IPresentationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public SchedulePresentationCommandHandler(
        IPresentationRepository repository,
        ITenantContext tenantContext,
        IClock clock)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public Task<Guid> HandleAsync(SchedulePresentationCommand command, CancellationToken cancellationToken = default)
    {
        Presentation presentation = Presentation.Schedule(
            _tenantContext.CompanyId,
            command.Type,
            command.Title,
            command.Doi,
            command.PresenterName,
            command.ScheduledDate,
            command.Notes,
            _clock.UtcNow);

        _repository.Add(presentation);
        return Task.FromResult(presentation.Id);
    }
}
