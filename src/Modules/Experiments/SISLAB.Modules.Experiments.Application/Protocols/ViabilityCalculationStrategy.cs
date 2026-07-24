using System.Text.Json;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Protocols;

/// <summary>
/// The <c>viability@v1</c> protocol (decision card #68 — in vitro % cell viability). Computes, per treated
/// well, the percentage of viable cells relative to the untreated control, subtracting the plate background:
/// <code>
/// % viability = (Abs_well - Abs_blank_mean) / (Abs_control_mean - Abs_blank_mean) × 100
/// </code>
/// and returns an immutable <see cref="FormulaSnapshot"/> the aggregate stores as-is.
/// </summary>
/// <remarks>
/// <para>
/// This is the antidote to the spreadsheet <c>#ERROR!</c> the discovery found (card #68): the formula lives in
/// versioned, unit-tested code, and its output is frozen into the snapshot rather than recomputed on every read.
/// </para>
/// <para>
/// Validation is strict and fails fast with a domain error: every designed well must carry an imported
/// absorbance, the plate must have at least one <see cref="WellRole.Blank"/> and one <see cref="WellRole.Control"/>,
/// and the control-minus-blank denominator must be non-zero (guarding the exact division-by-zero that corrupts the
/// Excel version). The result JSON carries the two reference means and one entry per non-reference well.
/// </para>
/// </remarks>
internal sealed class ViabilityCalculationStrategy : IExperimentProtocol
{
    /// <summary>Versioned formula code stored on the snapshot.</summary>
    public const string FormulaCode = "viability@v1";

    /// <summary>Human-readable expression stored on the snapshot for traceability.</summary>
    public const string FormulaExpression =
        "% viability = (Abs_well - Abs_blank_mean) / (Abs_control_mean - Abs_blank_mean) * 100";

    private static readonly JsonSerializerOptions ResultSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public ExperimentType Type => ExperimentType.ViabilidadeCelular;

    /// <inheritdoc />
    public FormulaSnapshot Calculate(Experiment experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);

        if (experiment is not ViabilidadeCelularExperiment viability)
            throw new DomainException(
                $"The {FormulaCode} protocol only calculates viability experiments.");

        IReadOnlyList<Well> wells = viability.Plate.Wells;

        if (wells.Count == 0)
            throw new DomainException("The plate has no wells to calculate.");

        // Excluded outliers (SISLAB-06) are ignored everywhere below: a missing reading on an excluded well never
        // blocks the calculation, and the means/results are computed only from the replicates that still count.
        if (wells.Any(well => !well.HasReading && !well.IsExcluded))
            throw new DomainException(
                "Every designed well must have an imported absorbance before calculating viability.");

        decimal blankMean = Mean(wells, WellRole.Blank)
            ?? throw new DomainException("The plate must contain at least one blank well.");

        decimal controlMean = Mean(wells, WellRole.Control)
            ?? throw new DomainException("The plate must contain at least one control well.");

        decimal denominator = controlMean - blankMean;
        if (denominator == 0)
            throw new DomainException(
                "The control mean equals the blank mean (zero denominator): viability cannot be computed.");

        IReadOnlyList<WellViabilityResult> results = wells
            .Where(well => well.CountsTowardCalculation && well.Role is WellRole.Sample or WellRole.CurvePoint)
            .OrderBy(well => well.Row)
            .ThenBy(well => well.Column)
            .Select(well => new WellViabilityResult(
                well.Coordinate,
                well.Role.ToString(),
                well.SampleId,
                well.ConcentrationUm,
                well.RawAbsorbance!.Value,
                ViabilityPercent(well.RawAbsorbance!.Value, blankMean, denominator)))
            .ToList();

        IReadOnlyList<ConditionAggregate> conditions = AggregateByCondition(results);

        var payload = new ViabilityResultPayload(
            FormulaCode,
            Math.Round(blankMean, 4),
            Math.Round(controlMean, 4),
            results,
            conditions);

        string resultJson = JsonSerializer.Serialize(payload, ResultSerializerOptions);

        return FormulaSnapshot.Create(FormulaCode, FormulaExpression, DateTime.UtcNow, resultJson);
    }

    private static decimal? Mean(IReadOnlyList<Well> wells, WellRole role)
    {
        List<decimal> values = wells
            .Where(well => well.Role == role && well.CountsTowardCalculation)
            .Select(well => well.RawAbsorbance!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Average();
    }

    private static decimal ViabilityPercent(decimal absorbance, decimal blankMean, decimal denominator)
        => Math.Round((absorbance - blankMean) / denominator * 100m, 2);

    /// <summary>
    /// Groups the per-well results into conditions (SISLAB-07 — same compound × concentration = replicates) and
    /// computes the replicate count, mean and sample SD of the % viability per condition. Only wells that count
    /// toward the calculation reached <paramref name="results"/>, so excluded outliers are already out of the mean.
    /// </summary>
    private static IReadOnlyList<ConditionAggregate> AggregateByCondition(IReadOnlyList<WellViabilityResult> results)
        => results
            .GroupBy(result => new ConditionKey(result.SampleId, result.ConcentrationUm))
            .OrderBy(group => group.Key.ConcentrationUm ?? decimal.MaxValue)
            .ThenBy(group => group.Key.SampleId)
            .Select(group =>
            {
                (int count, decimal mean, decimal? standardDeviation) =
                    ReplicateStatistics.Compute(group.Select(result => result.ViabilityPct).ToList());

                return new ConditionAggregate(
                    group.Key.SampleId,
                    group.Key.ConcentrationUm,
                    count,
                    Math.Round(mean, 2),
                    standardDeviation is { } sd ? Math.Round(sd, 2) : null,
                    group.Select(result => result.Well).ToList());
            })
            .ToList();

    private readonly record struct ConditionKey(string? SampleId, decimal? ConcentrationUm);

    /// <summary>Serialized result payload (the snapshot's frozen JSON). Web-cased to match the API shape.</summary>
    private sealed record ViabilityResultPayload(
        string Formula,
        decimal BlankMean,
        decimal ControlMean,
        IReadOnlyList<WellViabilityResult> Wells,
        IReadOnlyList<ConditionAggregate> Conditions);

    /// <summary>Per-well viability line inside the result payload.</summary>
    private sealed record WellViabilityResult(
        string Well,
        string Role,
        string? SampleId,
        decimal? ConcentrationUm,
        decimal RawAbsorbance,
        decimal ViabilityPct);

    /// <summary>
    /// Per-condition aggregate (SISLAB-07): the replicate count, mean and sample SD of % viability for one
    /// compound × concentration, plus the coordinates of the replicates that fed it.
    /// </summary>
    private sealed record ConditionAggregate(
        string? SampleId,
        decimal? ConcentrationUm,
        int ReplicateCount,
        decimal MeanViabilityPct,
        decimal? StdDevViabilityPct,
        IReadOnlyList<string> Wells);
}
