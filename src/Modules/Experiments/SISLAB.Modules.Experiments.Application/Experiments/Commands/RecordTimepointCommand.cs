using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Records the readings of a single timepoint on a behavioural experiment (card [E11] #88): one reading per
/// animal, authored by the current actor. The aggregate marks the timepoint step as performed and advances the
/// experiment to <see cref="ExperimentStatus.AwaitingCalculation"/> — the in vivo hand-off to the calculator.
/// </summary>
/// <remarks>
/// The handler is type-agnostic: it loads the shared <see cref="BehavioralExperiment"/> and delegates to it. The
/// "who" comes from the audit actor accessor, the "when" from the clock — never the request.
/// </remarks>
public sealed record RecordTimepointCommand(
    Guid ExperimentId,
    string TimepointLabel,
    IReadOnlyList<TimepointReading> Readings) : ICommand;

/// <summary>One animal's raw reading at the timepoint (the animal id by value + the raw value string).</summary>
public sealed record TimepointReading(Guid AnimalId, string RawValue);

internal sealed class RecordTimepointCommandValidator : AbstractValidator<RecordTimepointCommand>
{
    public RecordTimepointCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.TimepointLabel).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Readings).NotEmpty();
        RuleForEach(command => command.Readings).ChildRules(reading =>
        {
            reading.RuleFor(r => r.AnimalId).NotEmpty();
            reading.RuleFor(r => r.RawValue).NotEmpty().MaximumLength(500);
        });
    }
}

internal sealed class RecordTimepointCommandHandler : ICommandHandler<RecordTimepointCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public RecordTimepointCommandHandler(
        IExperimentRepository experiments,
        IAuditActorAccessor actorAccessor,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _experiments = experiments;
        _actorAccessor = actorAccessor;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Unit> HandleAsync(RecordTimepointCommand request, CancellationToken cancellationToken = default)
    {
        BehavioralExperiment experiment =
            await _experiments.FindBehavioralExperimentAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Behavioural experiment '{request.ExperimentId}' was not found.");

        // Responsibility gate (card [E11]): the lead or the responsible of this specific timepoint step may record.
        Guid? timepointStepId = experiment
            .FindStep(ExperimentStepKind.Timepoint, request.TimepointLabel)?.Id;
        experiment.EnsureCanBeEditedBy(_currentUser.RequireUserId(), timepointStepId);

        experiment.RecordTimepoint(
            request.TimepointLabel,
            request.Readings.Select(reading => (reading.AnimalId, reading.RawValue)),
            _actorAccessor.GetCurrentActor(),
            _clock.UtcNow);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
