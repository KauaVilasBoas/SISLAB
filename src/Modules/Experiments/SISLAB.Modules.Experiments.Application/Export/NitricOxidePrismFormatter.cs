using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Prism CSV formatter for <c>nitric-oxide@v1</c> (card [E11] #79). Emits the Griess result as two stacked
/// blocks Prism accepts by paste: the calibration curve (concentration × baseline-corrected absorbance) and then
/// the samples (well × absorbance × computed NO µM).
/// </summary>
/// <remarks>
/// It reads only the frozen snapshot JSON produced by <c>NitricOxideCalculationStrategy</c> — never recomputes.
/// Curve rows have no computed NO (they define the line), so that cell is an em dash; sample rows carry the NO
/// concentration read off the fitted line.
/// </remarks>
internal sealed class NitricOxidePrismFormatter : IPrismCsvFormatter
{
    private const string NotApplicable = "—";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public string FormulaCode => "nitric-oxide@v1";

    /// <inheritdoc />
    public string Format(string resultJson)
    {
        NitricOxidePayload payload =
            JsonSerializer.Deserialize<NitricOxidePayload>(resultJson, Options)
            ?? new NitricOxidePayload(null, null, null);

        IReadOnlyList<CurvePoint> curve = payload.Curve ?? [];
        IReadOnlyList<SampleWell> samples = payload.Wells ?? [];

        var csv = new StringBuilder();

        csv.Append("Concentração (µM),Absorbância,NO Calculado (µM)\n");
        foreach (CurvePoint point in curve)
        {
            csv.Append(Format(point.ConcentrationUm));
            csv.Append(',');
            csv.Append(Format(point.Absorbance));
            csv.Append(',');
            csv.Append(NotApplicable);
            csv.Append('\n');
        }

        csv.Append("Amostras,Absorbância,NO Calculado (µM)\n");
        foreach (SampleWell sample in samples)
        {
            csv.Append(Csv.Escape(sample.Well));
            csv.Append(',');
            csv.Append(Format(sample.RawAbsorbance));
            csv.Append(',');
            csv.Append(Format(sample.ConcentrationUm));
            csv.Append('\n');
        }

        AppendConditionSummary(csv, payload.Conditions ?? []);

        return csv.ToString();
    }

    /// <summary>
    /// Appends the per-condition summary block (SISLAB-07): one row per compound with its replicate count, mean
    /// and sample SD of the computed NO µM, read straight from the frozen snapshot (never recomputed). Kept as a
    /// third block after the curve and samples so the existing Prism paste layout is unchanged.
    /// </summary>
    private static void AppendConditionSummary(StringBuilder csv, IReadOnlyList<NitricOxideCondition> conditions)
    {
        if (conditions.Count == 0)
            return;

        csv.Append("Resumo por condição,N,Média NO (µM),Desvio NO (µM)\n");
        foreach (NitricOxideCondition condition in conditions)
        {
            csv.Append(Csv.Escape(condition.SampleId ?? "—"));
            csv.Append(',');
            csv.Append(condition.ReplicateCount.ToString(CultureInfo.InvariantCulture));
            csv.Append(',');
            csv.Append(Format(condition.MeanConcentrationUm));
            csv.Append(',');
            csv.Append(condition.StdDevConcentrationUm is { } sd ? Format(sd) : NotApplicable);
            csv.Append('\n');
        }
    }

    private static string Format(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private sealed record NitricOxidePayload(
        IReadOnlyList<CurvePoint>? Curve,
        IReadOnlyList<SampleWell>? Wells,
        IReadOnlyList<NitricOxideCondition>? Conditions);

    private sealed record CurvePoint(decimal ConcentrationUm, decimal Absorbance);

    private sealed record SampleWell(string Well, decimal RawAbsorbance, decimal ConcentrationUm);

    private sealed record NitricOxideCondition(
        string? SampleId,
        int ReplicateCount,
        decimal MeanConcentrationUm,
        decimal? StdDevConcentrationUm);
}
