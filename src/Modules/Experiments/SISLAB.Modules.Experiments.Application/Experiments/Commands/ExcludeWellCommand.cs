using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Marks a plate well as an excluded outlier before the calculation runs (SISLAB-06 — the operator's visual,
/// human decision to drop a replicate that is "muito fora do padrão das outras replicatas"). The exclusion is
/// recorded with the operator's reason and author and is honoured by both plate strategies (viability and nitric
/// oxide): the well leaves every control/blank mean, the calibration curve and the per-well results.
/// </summary>
/// <remarks>
/// Rejected once the calculation snapshot is frozen — the result is immutable, so the replicate set it was
/// computed from cannot change afterwards (reproducibility, enforced by the aggregate).
/// </remarks>
public sealed record ExcludeWellCommand(
    Guid ExperimentId,
    string Coordinate,
    string Reason) : ICommand;

internal sealed class ExcludeWellCommandValidator : AbstractValidator<ExcludeWellCommand>
{
    public ExcludeWellCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.Coordinate).NotEmpty().MaximumLength(4);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class ExcludeWellCommandHandler : ICommandHandler<ExcludeWellCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly ICurrentUserContext _currentUser;

    public ExcludeWellCommandHandler(
        IExperimentRepository experiments,
        IAuditActorAccessor actorAccessor,
        ICurrentUserContext currentUser)
    {
        _experiments = experiments;
        _actorAccessor = actorAccessor;
        _currentUser = currentUser;
    }

    public async Task<Unit> HandleAsync(ExcludeWellCommand request, CancellationToken cancellationToken = default)
    {
        PlateExperiment experiment =
            await _experiments.FindPlateExperimentWithPlateAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Plate experiment '{request.ExperimentId}' was not found.");

        // Responsibility gate (card [E11]): the lead or the calculation step's responsible curates the replicates.
        experiment.EnsureCanBeEditedBy(_currentUser.RequireUserId(), ExperimentStepKind.Calculation);

        experiment.ExcludeWell(request.Coordinate, request.Reason, _actorAccessor.GetCurrentActor());

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
