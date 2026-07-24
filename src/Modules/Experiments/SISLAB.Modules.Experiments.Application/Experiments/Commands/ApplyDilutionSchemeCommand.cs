using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.Modules.Experiments.Domain.Preparations;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Populates a plate column's <c>ConcentrationUm</c> wells from a computed serial-dilution scheme (SISLAB-05) — the
/// "O esquema gerado popula as ConcentrationUm dos poços" acceptance criterion. It reuses the pure
/// <see cref="SerialDilutionCalculator"/> to build the series (never re-implementing the formula) and then asks the
/// aggregate to stamp it onto the designed column, top row down. Kept as a dedicated command rather than overloading
/// <c>DesignPlateCommand</c> so the plate can be laid out once and then have a series applied to any column without
/// re-sending the whole 96-well design.
/// </summary>
/// <remarks>
/// The plate must already be designed and the column must have exactly as many wells as the series has points
/// (guarded by the aggregate). Every laboratory-specific value — the top concentration, factor, number of points,
/// final volume and the "half in the well" doubling — is a parameter, exactly as on the stateless compute query.
/// </remarks>
public sealed record ApplyDilutionSchemeCommand(
    Guid ExperimentId,
    int Column,
    decimal TopConcentrationMicromolar,
    decimal Factor,
    int NumberOfPoints,
    decimal FinalVolumeMicrolitres,
    bool DoubleForHalfInWell) : ICommand;

internal sealed class ApplyDilutionSchemeCommandValidator : AbstractValidator<ApplyDilutionSchemeCommand>
{
    public ApplyDilutionSchemeCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.Column).InclusiveBetween(1, Plate.Columns);
        RuleFor(command => command.TopConcentrationMicromolar).GreaterThan(0);
        RuleFor(command => command.Factor).GreaterThan(1);
        RuleFor(command => command.NumberOfPoints).GreaterThanOrEqualTo(1);
        RuleFor(command => command.FinalVolumeMicrolitres).GreaterThan(0);
    }
}

internal sealed class ApplyDilutionSchemeCommandHandler : ICommandHandler<ApplyDilutionSchemeCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly ICurrentUserContext _currentUser;

    public ApplyDilutionSchemeCommandHandler(IExperimentRepository experiments, ICurrentUserContext currentUser)
    {
        _experiments = experiments;
        _currentUser = currentUser;
    }

    public async Task<Unit> HandleAsync(ApplyDilutionSchemeCommand request, CancellationToken cancellationToken = default)
    {
        PlateExperiment experiment =
            await _experiments.FindPlateExperimentWithPlateAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Plate experiment '{request.ExperimentId}' was not found.");

        // Responsibility gate: the lead or the design step's responsible may lay out concentrations (same as design).
        experiment.EnsureCanBeEditedBy(_currentUser.RequireUserId(), ExperimentStepKind.Baseline);

        // Reuse the pure formula — the series is a function of the inputs, never re-implemented here.
        SerialDilutionScheme scheme = SerialDilutionCalculator.Build(
            request.TopConcentrationMicromolar,
            request.Factor,
            request.NumberOfPoints,
            request.FinalVolumeMicrolitres,
            request.DoubleForHalfInWell);

        experiment.ApplyConcentrationScheme(request.Column, scheme.Concentrations);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
