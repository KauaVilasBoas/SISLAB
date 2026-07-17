using System.Text.Json;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Protocols;

/// <summary>
/// The <c>nitric-oxide@v1</c> protocol (card [E11] #72 — in vitro nitric oxide by the Griess reaction). It fits a
/// calibration curve from the plate's <see cref="WellRole.Standard"/> wells (sodium-nitrite points of known µM)
/// by ordinary least squares:
/// <code>
/// absorbance = a * concentration + b        (fitted from the standards, baseline-corrected by the blank mean)
/// concentration_NO = (absorbance - b) / a   (solved per sample well)
/// </code>
/// and returns an immutable <see cref="FormulaSnapshot"/> the aggregate stores as-is.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the viability protocol structure (Strategy resolved by type, frozen snapshot — the antidote to the
/// spreadsheet <c>#ERROR!</c>) but with the Griess algorithm: the standards define the line, every sample's NO
/// concentration is read off it. The blank wells are excluded from the fit and their mean is used as the baseline
/// subtracted from every absorbance before fitting/solving.
/// </para>
/// <para>
/// Validation is strict and fails fast with a domain error: every designed well must carry an imported absorbance,
/// there must be at least two standards with a concentration, and the fitted slope must be non-zero (a flat curve
/// cannot invert to a concentration). The coefficient of determination R² is reported on the snapshot; a poor fit
/// (R² &lt; 0.95) is surfaced as a warning in the result payload, not an error — the operator decides whether to
/// trust the run.
/// </para>
/// </remarks>
internal sealed class NitricOxideCalculationStrategy : IExperimentProtocol
{
    /// <summary>Versioned formula code stored on the snapshot.</summary>
    public const string FormulaCode = "nitric-oxide@v1";

    /// <summary>Human-readable expression stored on the snapshot for traceability.</summary>
    public const string FormulaExpression =
        "absorbance = a * concentration + b (least squares over standards); NO = (absorbance - b) / a";

    /// <summary>Minimum coefficient of determination below which the fit is flagged as a low-confidence warning.</summary>
    public const decimal MinAcceptableRSquared = 0.95m;

    private const int MinStandards = 2;

    private static readonly JsonSerializerOptions ResultSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public ExperimentType Type => ExperimentType.NitricOxide;

    /// <inheritdoc />
    public FormulaSnapshot Calculate(Experiment experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);

        if (experiment is not NitricOxideExperiment nitricOxide)
            throw new DomainException(
                $"The {FormulaCode} protocol only calculates nitric-oxide experiments.");

        IReadOnlyList<Well> wells = nitricOxide.Plate.Wells;

        if (wells.Count == 0)
            throw new DomainException("The plate has no wells to calculate.");

        if (wells.Any(well => !well.HasReading))
            throw new DomainException(
                "Every designed well must have an imported absorbance before calculating nitric oxide.");

        decimal baseline = Mean(wells, WellRole.Blank) ?? 0m;

        IReadOnlyList<CurveDataPoint> standards = wells
            .Where(well => well.Role == WellRole.Standard && well.ConcentrationUm.HasValue)
            .Select(well => new CurveDataPoint(well.ConcentrationUm!.Value, well.RawAbsorbance!.Value - baseline))
            .OrderBy(point => point.Concentration)
            .ToList();

        if (standards.Count < MinStandards)
            throw new DomainException(
                $"The Griess calibration curve needs at least {MinStandards} standard wells with a known " +
                $"concentration; found {standards.Count}.");

        LinearFit fit = FitLine(standards);

        if (fit.Slope == 0)
            throw new DomainException(
                "The calibration curve is flat (zero slope): a nitric-oxide concentration cannot be derived.");

        IReadOnlyList<NitricOxideWellResult> sampleResults = wells
            .Where(well => well.Role == WellRole.Sample)
            .OrderBy(well => well.Row)
            .ThenBy(well => well.Column)
            .Select(well => new NitricOxideWellResult(
                well.Coordinate,
                well.Role.ToString(),
                well.RawAbsorbance!.Value,
                ConcentrationFrom(well.RawAbsorbance!.Value - baseline, fit)))
            .ToList();

        IReadOnlyList<NitricOxideCurvePoint> curve = standards
            .Select(point => new NitricOxideCurvePoint(point.Concentration, point.Absorbance))
            .ToList();

        var payload = new NitricOxideResultPayload(
            FormulaCode,
            Math.Round(fit.Slope, 6),
            Math.Round(fit.Intercept, 6),
            Math.Round(fit.RSquared, 4),
            fit.RSquared < MinAcceptableRSquared,
            Math.Round(baseline, 4),
            curve,
            sampleResults);

        string resultJson = JsonSerializer.Serialize(payload, ResultSerializerOptions);

        return FormulaSnapshot.Create(FormulaCode, FormulaExpression, DateTime.UtcNow, resultJson);
    }

    /// <summary>
    /// Ordinary least-squares fit of <c>y = a*x + b</c> over the curve points, plus the coefficient of
    /// determination R². Implemented inline (no external numerics dependency) over the small standards set.
    /// </summary>
    private static LinearFit FitLine(IReadOnlyList<CurveDataPoint> points)
    {
        int n = points.Count;
        decimal sumX = points.Sum(p => p.Concentration);
        decimal sumY = points.Sum(p => p.Absorbance);
        decimal sumXy = points.Sum(p => p.Concentration * p.Absorbance);
        decimal sumXx = points.Sum(p => p.Concentration * p.Concentration);

        decimal denominator = n * sumXx - sumX * sumX;
        if (denominator == 0)
            return new LinearFit(Slope: 0m, Intercept: sumY / n, RSquared: 0m);

        decimal slope = (n * sumXy - sumX * sumY) / denominator;
        decimal intercept = (sumY - slope * sumX) / n;

        decimal meanY = sumY / n;
        decimal totalSumSquares = points.Sum(p => (p.Absorbance - meanY) * (p.Absorbance - meanY));
        decimal residualSumSquares = points.Sum(p =>
        {
            decimal predicted = slope * p.Concentration + intercept;
            decimal residual = p.Absorbance - predicted;
            return residual * residual;
        });

        decimal rSquared = totalSumSquares == 0 ? 1m : 1m - residualSumSquares / totalSumSquares;

        return new LinearFit(slope, intercept, rSquared);
    }

    private static decimal ConcentrationFrom(decimal correctedAbsorbance, LinearFit fit)
        => Math.Round((correctedAbsorbance - fit.Intercept) / fit.Slope, 4);

    private static decimal? Mean(IReadOnlyList<Well> wells, WellRole role)
    {
        List<decimal> values = wells
            .Where(well => well.Role == role)
            .Select(well => well.RawAbsorbance!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Average();
    }

    private readonly record struct CurveDataPoint(decimal Concentration, decimal Absorbance);

    private readonly record struct LinearFit(decimal Slope, decimal Intercept, decimal RSquared);

    /// <summary>Serialized result payload (the snapshot's frozen JSON). Web-cased to match the API shape.</summary>
    private sealed record NitricOxideResultPayload(
        string Formula,
        decimal Slope,
        decimal Intercept,
        decimal RSquared,
        bool LowConfidence,
        decimal BlankBaseline,
        IReadOnlyList<NitricOxideCurvePoint> Curve,
        IReadOnlyList<NitricOxideWellResult> Wells);

    /// <summary>A baseline-corrected calibration point inside the result payload.</summary>
    private sealed record NitricOxideCurvePoint(decimal ConcentrationUm, decimal Absorbance);

    /// <summary>Per-sample NO result line inside the result payload.</summary>
    private sealed record NitricOxideWellResult(
        string Well,
        string Role,
        decimal RawAbsorbance,
        decimal ConcentrationUm);
}
