using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Imports the plate reader's raw absorbance into a viability experiment (decision card #68 — "import matriz
/// 8×12 de absorbância"). The <paramref name="CsvContent"/> is the canonical <c>well,absorbance</c> CSV the
/// operator pastes from the reader; every well it names must belong to the designed plate. Marks the reader
/// import step as performed.
/// </summary>
public sealed record ImportPlateReadingCommand(
    Guid ExperimentId,
    string CsvContent) : ICommand;

internal sealed class ImportPlateReadingCommandValidator : AbstractValidator<ImportPlateReadingCommand>
{
    public ImportPlateReadingCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.CsvContent).NotEmpty();
    }
}

internal sealed class ImportPlateReadingCommandHandler : ICommandHandler<ImportPlateReadingCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public ImportPlateReadingCommandHandler(
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

    public async Task<Unit> HandleAsync(
        ImportPlateReadingCommand request,
        CancellationToken cancellationToken = default)
    {
        PlateExperiment experiment =
            await _experiments.FindPlateExperimentWithPlateAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Plate experiment '{request.ExperimentId}' was not found.");

        // Responsibility gate (card [E11]): the lead or the reader-import step's responsible may import.
        experiment.EnsureCanBeEditedBy(_currentUser.RequireUserId(), ExperimentStepKind.Measurement);

        IReadOnlyList<PlateReading> readings = PlateReadingCsvParser.Parse(request.CsvContent);

        foreach (PlateReading reading in readings)
            experiment.RecordWellAbsorbance(reading.Coordinate, reading.Absorbance);

        experiment.MarkReadingImported(_actorAccessor.GetCurrentActor(), _clock.UtcNow);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
