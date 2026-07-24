using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Records a recurring physiological reading (glicemia/peso, … — SISLAB-02) on a project animal at a timepoint. The
/// <paramref name="ParameterCode"/> and <paramref name="Unit"/> are free-text cadaster values (nothing lab-specific
/// is fixed here); the timepoint is the label the reading was taken at (basal, pós-indução, 7/15/21/28 dias). The
/// author comes from the audit actor accessor and the instant from the clock — never the request body. Returns the
/// new reading id.
/// </summary>
public sealed record RecordPhysiologicalReadingCommand(
    Guid ProjectId,
    Guid AnimalId,
    string ParameterCode,
    decimal Value,
    string Unit,
    string TimepointLabel) : ICommand<Guid>;

internal sealed class RecordPhysiologicalReadingCommandValidator
    : AbstractValidator<RecordPhysiologicalReadingCommand>
{
    public RecordPhysiologicalReadingCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.AnimalId).NotEmpty();
        RuleFor(command => command.ParameterCode).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Unit).NotEmpty().MaximumLength(30);
        RuleFor(command => command.TimepointLabel).NotEmpty().MaximumLength(60);
    }
}

internal sealed class RecordPhysiologicalReadingCommandHandler
    : ICommandHandler<RecordPhysiologicalReadingCommand, Guid>
{
    private readonly IProjectRepository _projects;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public RecordPhysiologicalReadingCommandHandler(
        IProjectRepository projects,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _projects = projects;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(
        RecordPhysiologicalReadingCommand request,
        CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        PhysiologicalReading reading = project.RecordPhysiologicalReading(
            request.AnimalId,
            request.ParameterCode,
            request.Value,
            request.Unit,
            request.TimepointLabel,
            _actorAccessor.GetCurrentActor(),
            _clock.UtcNow);

        await _projects.UpdateAsync(project, cancellationToken);

        return reading.Id;
    }
}
