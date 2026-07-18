using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Runs the versioned calculation over a behavioural experiment's recorded timepoints (card [E11] #88): resolves
/// the type's protocol (e.g. <c>von-frey-up-down@v1</c>) from the registry, applies it, freezes the result as an
/// immutable snapshot on the aggregate and advances the experiment to
/// <see cref="ExperimentStatus.AwaitingAnalysis"/>. Tests that need no calculation still run a trivial protocol
/// that simply confirms the dataset (kept out of this command — those types have no registered protocol and are
/// exported from the raw values directly).
/// </summary>
/// <remarks>
/// The handler is type-agnostic: it loads the shared <see cref="BehavioralExperiment"/> and delegates to the
/// Strategy resolved by <see cref="Experiment.Type"/>, so adding a scorable assay is a new protocol registration,
/// never an edit here (Strategy + registry, decision card #68).
/// </remarks>
public sealed record CalculateBehavioralExperimentCommand(Guid ExperimentId) : ICommand;

internal sealed class CalculateBehavioralExperimentCommandValidator
    : AbstractValidator<CalculateBehavioralExperimentCommand>
{
    public CalculateBehavioralExperimentCommandValidator()
        => RuleFor(command => command.ExperimentId).NotEmpty();
}

internal sealed class CalculateBehavioralExperimentCommandHandler
    : ICommandHandler<CalculateBehavioralExperimentCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly IExperimentProtocolResolver _protocolResolver;
    private readonly IAuditActorAccessor _actorAccessor;

    public CalculateBehavioralExperimentCommandHandler(
        IExperimentRepository experiments,
        IExperimentProtocolResolver protocolResolver,
        IAuditActorAccessor actorAccessor)
    {
        _experiments = experiments;
        _protocolResolver = protocolResolver;
        _actorAccessor = actorAccessor;
    }

    public async Task<Unit> HandleAsync(
        CalculateBehavioralExperimentCommand request,
        CancellationToken cancellationToken = default)
    {
        BehavioralExperiment experiment =
            await _experiments.FindBehavioralExperimentAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Behavioural experiment '{request.ExperimentId}' was not found.");

        IExperimentProtocol protocol = _protocolResolver.Resolve(experiment.Type);
        FormulaSnapshot snapshot = protocol.Calculate(experiment);

        experiment.ApplyCalculation(snapshot, _actorAccessor.GetCurrentActor());

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
