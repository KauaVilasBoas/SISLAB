using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Creates a new in vivo behavioural experiment of the requested <paramref name="Type"/> (card [E11] #88 — von
/// Frey / tail-flick / rota-rod / hemogram) bound to a project and batch (held by value), with its timepoint flow
/// seeded from <paramref name="TimepointLabels"/>. Returns the new experiment id.
/// </summary>
/// <remarks>
/// A single command serves every behavioural assay: the subtypes differ only by their factory and their versioned
/// calculation Strategy, so branching here on <see cref="ExperimentType"/> keeps the write surface small. The
/// company is never in the payload — it is stamped by the write-side tenant machinery on <c>SaveChanges</c>; the
/// creator comes from the audit actor accessor. The aggregate owns trimming and seeds its steps.
/// </remarks>
public sealed record CreateBehavioralExperimentCommand(
    ExperimentType Type,
    string Title,
    string? Description,
    Guid ProjectId,
    Guid BatchId,
    IReadOnlyList<string> TimepointLabels) : ICommand<Guid>;

internal sealed class CreateBehavioralExperimentCommandValidator
    : AbstractValidator<CreateBehavioralExperimentCommand>
{
    private static readonly ExperimentType[] BehavioralTypes =
    [
        ExperimentType.VonFrei,
        ExperimentType.TailFlick,
        ExperimentType.RotaRod,
        ExperimentType.Hemograma,
    ];

    public CreateBehavioralExperimentCommandValidator()
    {
        RuleFor(command => command.Type)
            .Must(type => BehavioralTypes.Contains(type))
            .WithMessage("The experiment type must be an in vivo behavioural assay.");
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(2000);
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.TimepointLabels).NotEmpty();
        RuleForEach(command => command.TimepointLabels).NotEmpty().MaximumLength(60);
    }
}

internal sealed class CreateBehavioralExperimentCommandHandler
    : ICommandHandler<CreateBehavioralExperimentCommand, Guid>
{
    private readonly IExperimentRepository _experiments;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public CreateBehavioralExperimentCommandHandler(
        IExperimentRepository experiments,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _experiments = experiments;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(
        CreateBehavioralExperimentCommand request,
        CancellationToken cancellationToken = default)
    {
        string actor = _actorAccessor.GetCurrentActor();

        Experiment experiment = request.Type switch
        {
            ExperimentType.VonFrei => VonFreiExperiment.Create(
                request.Title, request.Description, actor, _clock.UtcNow,
                request.ProjectId, request.BatchId, request.TimepointLabels),
            ExperimentType.TailFlick => TailFlickExperiment.Create(
                request.Title, request.Description, actor, _clock.UtcNow,
                request.ProjectId, request.BatchId, request.TimepointLabels),
            ExperimentType.RotaRod => RotaRodExperiment.Create(
                request.Title, request.Description, actor, _clock.UtcNow,
                request.ProjectId, request.BatchId, request.TimepointLabels),
            ExperimentType.Hemograma => HemogramaExperiment.Create(
                request.Title, request.Description, actor, _clock.UtcNow,
                request.ProjectId, request.BatchId, request.TimepointLabels),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request), request.Type, "Unsupported behavioural experiment type."),
        };

        await _experiments.AddAsync(experiment, cancellationToken);

        return experiment.Id;
    }
}
