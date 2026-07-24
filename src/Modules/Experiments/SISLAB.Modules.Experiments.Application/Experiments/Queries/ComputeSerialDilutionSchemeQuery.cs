using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Preparations;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Experiments.Queries;

/// <summary>
/// Stateless compute (SISLAB-05) that turns a compound definition, a top concentration, a dilution factor, a number
/// of points and a fixed final volume into the serial-dilution scheme the operator pipettes — the antidote to the
/// by-hand mother-plate column of the in vitro spreadsheet. It reuses the pure <see cref="SerialDilutionCalculator"/>
/// (and, when a compound is supplied, <see cref="StockSolution"/>), returning the series + per-point transfer/diluent
/// volumes + optional stock + optional DMSO fractions for the UI. It reads and writes no state, so it is modelled as a
/// query with an in-memory handler — no database, no tenant scoping (the numbers are a pure function of the inputs).
/// </summary>
/// <remarks>
/// Every laboratory-specific value is an input, never a constant: the factor (2/4/…), the top concentration, the
/// number of points, the final volume (600/800 µL), the "half in the well" doubling and the compound's molar mass (or
/// its mg/mL pesagem when it has none). The <see cref="StockMolarMassGramsPerMole"/>/<see cref="StockMassMilligrams"/>/
/// <see cref="StockTargetMolarityMicromolar"/> and mg/mL fields are optional: when present, the handler also computes
/// the stock solution the series steps down from.
/// </remarks>
public sealed record ComputeSerialDilutionSchemeQuery(
    decimal TopConcentrationMicromolar,
    decimal Factor,
    int NumberOfPoints,
    decimal FinalVolumeMicrolitres,
    bool DoubleForHalfInWell = false)
    : IQuery<SerialDilutionSchemeResult>
{
    /// <summary>Compound molar mass (g/mol) for the <c>V = m × M / MM</c> stock route; null on the mg/mL route.</summary>
    public decimal? StockMolarMassGramsPerMole { get; init; }

    /// <summary>Mass weighed for the stock (mg) on the molar-mass route; required together with the molar mass.</summary>
    public decimal? StockMassMilligrams { get; init; }

    /// <summary>Target stock molarity (µM) on the molar-mass route; required together with the molar mass.</summary>
    public decimal? StockTargetMolarityMicromolar { get; init; }

    /// <summary>Stock mass concentration (mg/mL) for a compound without a molar mass; the mg/mL route.</summary>
    public decimal? StockConcentrationMilligramsPerMillilitre { get; init; }

    /// <summary>Stock volume made up (mL) for the mg/mL route; required together with the mg/mL concentration.</summary>
    public decimal? StockVolumeMillilitres { get; init; }

    /// <summary>Pure DMSO volume in the mother solution (µL) for the DMSO fraction; null to skip the DMSO control.</summary>
    public decimal? DmsoMicrolitres { get; init; }

    /// <summary>Total mother-solution volume the DMSO is dissolved in (µL); required with <see cref="DmsoMicrolitres"/>.</summary>
    public decimal? DmsoSolutionMicrolitres { get; init; }

    /// <summary>How much the solution is further diluted in the well (1 = neat, 2 = half + half medium). Defaults to 1.</summary>
    public decimal DmsoInWellDilutionRatio { get; init; } = 1m;
}

/// <summary>One point of the computed series (SISLAB-05): concentration + how to reach it. Flat DTO for the UI.</summary>
public sealed record SerialDilutionSchemeStepResult(
    int Index,
    decimal ConcentrationMicromolar,
    decimal? TransferMicrolitres,
    decimal? DiluentMicrolitres,
    decimal FinalVolumeMicrolitres);

/// <summary>The stock solution the series steps down from (SISLAB-05), when a compound was supplied. Flat DTO.</summary>
public sealed record StockSolutionResult(
    decimal MassMilligrams,
    decimal? MolarMassGramsPerMole,
    decimal? ConcentrationMicromolar,
    decimal ConcentrationMilligramsPerMillilitre,
    decimal VolumeMillilitres);

/// <summary>The DMSO control (SISLAB-05): the solvent fraction in the mother solution and once in the well. Flat DTO.</summary>
public sealed record DmsoDilutionResult(
    decimal DmsoMicrolitres,
    decimal SolutionMicrolitres,
    decimal SolutionFraction,
    decimal WellFraction);

/// <summary>
/// The computed serial-dilution scheme (SISLAB-05): the factor, the fixed final volume, the ordered points and — when
/// supplied — the stock solution and DMSO control. The flat shape is what the SPA renders and what the plate-populate
/// endpoint consumes; it never leaks the domain value objects.
/// </summary>
public sealed record SerialDilutionSchemeResult(
    decimal Factor,
    decimal FinalVolumeMicrolitres,
    IReadOnlyList<SerialDilutionSchemeStepResult> Steps,
    StockSolutionResult? Stock,
    DmsoDilutionResult? Dmso);

internal sealed class ComputeSerialDilutionSchemeQueryValidator : AbstractValidator<ComputeSerialDilutionSchemeQuery>
{
    public ComputeSerialDilutionSchemeQueryValidator()
    {
        RuleFor(query => query.TopConcentrationMicromolar).GreaterThan(0);
        RuleFor(query => query.Factor).GreaterThan(1);
        RuleFor(query => query.NumberOfPoints).GreaterThanOrEqualTo(1);
        RuleFor(query => query.FinalVolumeMicrolitres).GreaterThan(0);
        RuleFor(query => query.DmsoInWellDilutionRatio).GreaterThan(0);
    }
}

internal sealed class ComputeSerialDilutionSchemeQueryHandler
    : IQueryHandler<ComputeSerialDilutionSchemeQuery, SerialDilutionSchemeResult>
{
    public Task<SerialDilutionSchemeResult> HandleAsync(
        ComputeSerialDilutionSchemeQuery request, CancellationToken cancellationToken = default)
    {
        SerialDilutionScheme scheme = SerialDilutionCalculator.Build(
            request.TopConcentrationMicromolar,
            request.Factor,
            request.NumberOfPoints,
            request.FinalVolumeMicrolitres,
            request.DoubleForHalfInWell);

        StockSolutionResult? stock = BuildStock(request);
        DmsoDilutionResult? dmso = BuildDmso(request);

        var steps = scheme.Steps
            .Select(step => new SerialDilutionSchemeStepResult(
                step.Index,
                step.ConcentrationMicromolar,
                step.TransferMicrolitres,
                step.DiluentMicrolitres,
                step.FinalVolumeMicrolitres))
            .ToList();

        return Task.FromResult(new SerialDilutionSchemeResult(
            scheme.Factor, scheme.FinalVolumeMicrolitres, steps, stock, dmso));
    }

    // The stock is optional: computed from a molar mass (V = m × M / MM) when the molar-mass triple is present,
    // else from a plain mg/mL pesagem when that pair is present, else omitted (the caller only wanted the series).
    private static StockSolutionResult? BuildStock(ComputeSerialDilutionSchemeQuery request)
    {
        if (request is { StockMolarMassGramsPerMole: { } molarMass, StockMassMilligrams: { } mass, StockTargetMolarityMicromolar: { } molarity })
            return ToResult(StockSolution.FromMolarMass(mass, molarMass, molarity));

        if (request is { StockConcentrationMilligramsPerMillilitre: { } mgPerMl, StockVolumeMillilitres: { } volume })
            return ToResult(StockSolution.FromMassConcentration(mgPerMl, volume));

        return null;
    }

    private static StockSolutionResult ToResult(StockSolution stock) => new(
        stock.MassMilligrams,
        stock.MolarMassGramsPerMole,
        stock.ConcentrationMicromolar,
        stock.ConcentrationMilligramsPerMillilitre,
        stock.VolumeMillilitres);

    // The DMSO control is optional: computed only when both the DMSO and the mother-solution volumes are supplied.
    private static DmsoDilutionResult? BuildDmso(ComputeSerialDilutionSchemeQuery request)
    {
        if (request is not { DmsoMicrolitres: { } dmso, DmsoSolutionMicrolitres: { } solution })
            return null;

        DmsoDilution dilution = DmsoDilution.Of(dmso, solution, request.DmsoInWellDilutionRatio);

        return new DmsoDilutionResult(
            dilution.DmsoMicrolitres,
            dilution.SolutionMicrolitres,
            dilution.SolutionFraction,
            dilution.WellFraction);
    }
}
