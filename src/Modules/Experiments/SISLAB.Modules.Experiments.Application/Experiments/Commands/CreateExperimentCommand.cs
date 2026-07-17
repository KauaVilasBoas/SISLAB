using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Creates a new plate experiment of the requested <paramref name="Type"/> for the active company (cards
/// [E11] #68 viability / #72 nitric oxide): its <paramref name="Title"/> (required), an optional
/// <paramref name="Description"/> and an optional <paramref name="CompoundPartnerId"/> — the partner compound
/// under test, held by value. Returns the new experiment id.
/// </summary>
/// <remarks>
/// A single command serves every plate assay: the two in vitro subtypes differ only by their factory and their
/// versioned calculation Strategy, so branching here on <see cref="ExperimentType"/> (rather than shipping a
/// near-identical command per type) keeps the write surface small. The company is never in the payload — it is
/// stamped by the write-side tenant machinery on <c>SaveChanges</c>. The creator ("who") comes from the audit
/// actor accessor, not the request. The aggregate owns trimming and seeds the default step flow, so the handler
/// only resolves the actor/clock and dispatches to the matching factory.
/// </remarks>
public sealed record CreateExperimentCommand(
    ExperimentType Type,
    string Title,
    string? Description,
    Guid? CompoundPartnerId) : ICommand<Guid>;

internal sealed class CreateExperimentCommandValidator
    : AbstractValidator<CreateExperimentCommand>
{
    public CreateExperimentCommandValidator()
    {
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(2000);
    }
}

internal sealed class CreateExperimentCommandHandler
    : ICommandHandler<CreateExperimentCommand, Guid>
{
    private readonly IExperimentRepository _experiments;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public CreateExperimentCommandHandler(
        IExperimentRepository experiments,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _experiments = experiments;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(
        CreateExperimentCommand request,
        CancellationToken cancellationToken = default)
    {
        string actor = _actorAccessor.GetCurrentActor();

        Experiment experiment = request.Type switch
        {
            ExperimentType.ViabilidadeCelular => ViabilidadeCelularExperiment.Create(
                request.Title, request.Description, actor, _clock.UtcNow, request.CompoundPartnerId),
            ExperimentType.NitricOxide => NitricOxideExperiment.Create(
                request.Title, request.Description, actor, _clock.UtcNow, request.CompoundPartnerId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request), request.Type, "Unsupported experiment type."),
        };

        await _experiments.AddAsync(experiment, cancellationToken);

        return experiment.Id;
    }
}
