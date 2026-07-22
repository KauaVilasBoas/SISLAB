using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Runs the versioned calculation over a plate experiment's imported reading (cards [E11] #68 / #72): resolves
/// the type's protocol (<c>viability@v1</c> or <c>nitric-oxide@v1</c>) from the registry, applies it, freezes the
/// result as an immutable snapshot on the aggregate and advances the experiment to
/// <see cref="ExperimentStatus.AwaitingAnalysis"/>. Applying the calculation also raises the domain event that,
/// via the Outbox, lets the Inventory cost report correlate to a calculated experiment (card #109).
/// </summary>
/// <remarks>
/// The handler is type-agnostic: it loads the shared <see cref="PlateExperiment"/> and delegates to the Strategy
/// resolved by <see cref="Experiment.Type"/>, so adding an assay type is a new protocol registration, never an
/// edit here (Strategy + registry, decision card #68).
/// </remarks>
public sealed record CalculateExperimentCommand(Guid ExperimentId) : ICommand;

internal sealed class CalculateExperimentCommandValidator : AbstractValidator<CalculateExperimentCommand>
{
    public CalculateExperimentCommandValidator()
        => RuleFor(command => command.ExperimentId).NotEmpty();
}

internal sealed class CalculateExperimentCommandHandler : ICommandHandler<CalculateExperimentCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly IExperimentProtocolResolver _protocolResolver;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly ICurrentUserContext _currentUser;

    public CalculateExperimentCommandHandler(
        IExperimentRepository experiments,
        IExperimentProtocolResolver protocolResolver,
        IAuditActorAccessor actorAccessor,
        ICurrentUserContext currentUser)
    {
        _experiments = experiments;
        _protocolResolver = protocolResolver;
        _actorAccessor = actorAccessor;
        _currentUser = currentUser;
    }

    public async Task<Unit> HandleAsync(
        CalculateExperimentCommand request,
        CancellationToken cancellationToken = default)
    {
        PlateExperiment experiment =
            await _experiments.FindPlateExperimentWithPlateAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Plate experiment '{request.ExperimentId}' was not found.");

        // Responsibility gate (card [E11]): the lead or the calculation step's responsible may calculate.
        experiment.EnsureCanBeEditedBy(_currentUser.RequireUserId(), ExperimentStepKind.Calculation);

        IExperimentProtocol protocol = _protocolResolver.Resolve(experiment.Type);
        FormulaSnapshot snapshot = protocol.Calculate(experiment);

        experiment.ApplyCalculation(snapshot, _actorAccessor.GetCurrentActor());

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
