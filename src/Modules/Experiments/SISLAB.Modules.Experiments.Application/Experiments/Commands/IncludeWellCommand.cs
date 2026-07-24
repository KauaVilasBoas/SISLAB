using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Brings a previously excluded well back into the calculation (SISLAB-06) — the inverse of
/// <see cref="ExcludeWellCommand"/>. Like exclusion, it is rejected once the snapshot is frozen.
/// </summary>
public sealed record IncludeWellCommand(
    Guid ExperimentId,
    string Coordinate) : ICommand;

internal sealed class IncludeWellCommandValidator : AbstractValidator<IncludeWellCommand>
{
    public IncludeWellCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.Coordinate).NotEmpty().MaximumLength(4);
    }
}

internal sealed class IncludeWellCommandHandler : ICommandHandler<IncludeWellCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly ICurrentUserContext _currentUser;

    public IncludeWellCommandHandler(
        IExperimentRepository experiments,
        ICurrentUserContext currentUser)
    {
        _experiments = experiments;
        _currentUser = currentUser;
    }

    public async Task<Unit> HandleAsync(IncludeWellCommand request, CancellationToken cancellationToken = default)
    {
        PlateExperiment experiment =
            await _experiments.FindPlateExperimentWithPlateAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Plate experiment '{request.ExperimentId}' was not found.");

        // Responsibility gate (card [E11]): the lead or the calculation step's responsible curates the replicates.
        experiment.EnsureCanBeEditedBy(_currentUser.RequireUserId(), ExperimentStepKind.Calculation);

        experiment.IncludeWell(request.Coordinate);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
