using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Prism CSV formatter for <c>viability@v1</c> (card [E11] #79). Lays the computed % viability out as a Prism
/// XY/grouped table: one column per tested concentration (µM), one row per replicate, so the operator pastes it
/// straight into a Prism dose-response layout.
/// </summary>
/// <remarks>
/// It reads only the frozen snapshot JSON produced by <c>ViabilityCalculationStrategy</c> — never recomputes —
/// so the export reflects exactly what was signed off. Wells are grouped by their concentration; within a group,
/// each well becomes a replicate row (ragged groups are padded with blanks so every column aligns).
/// </remarks>
internal sealed class ViabilityPrismFormatter : IPrismCsvFormatter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public string FormulaCode => "viability@v1";

    /// <inheritdoc />
    public string Format(string resultJson)
    {
        ViabilityPayload payload =
            JsonSerializer.Deserialize<ViabilityPayload>(resultJson, Options)
            ?? new ViabilityPayload(null, null);

        IReadOnlyList<ViabilityWell> wells = payload.Wells ?? [];

        // Group by concentration (nulls collapse to "Sem conc."), preserving ascending concentration order.
        var groups = wells
            .GroupBy(well => well.ConcentrationUm)
            .OrderBy(group => group.Key ?? decimal.MaxValue)
            .Select(group => new
            {
                Header = group.Key is { } concentration
                    ? $"{Format(concentration)} µM"
                    : "Sem concentração",
                Values = group.Select(well => well.ViabilityPct).ToList(),
            })
            .ToList();

        var csv = new StringBuilder();
        csv.Append("Composto");
        foreach (var group in groups)
        {
            csv.Append(',');
            csv.Append(Csv.Escape(group.Header));
        }
        csv.Append('\n');

        int replicates = groups.Count == 0 ? 0 : groups.Max(group => group.Values.Count);
        for (int replicate = 0; replicate < replicates; replicate++)
        {
            csv.Append(Csv.Escape($"Réplica {replicate + 1}"));
            foreach (var group in groups)
            {
                csv.Append(',');
                if (replicate < group.Values.Count)
                    csv.Append(Format(group.Values[replicate]));
            }
            csv.Append('\n');
        }

        AppendConditionSummary(csv, payload.Conditions ?? []);

        return csv.ToString();
    }

    /// <summary>
    /// Appends the per-condition summary block (SISLAB-07): one row per compound × concentration with its
    /// replicate count, mean and sample SD, read straight from the frozen snapshot (never recomputed). Kept as a
    /// second block after the replicate matrix so the existing Prism paste layout is unchanged.
    /// </summary>
    private static void AppendConditionSummary(StringBuilder csv, IReadOnlyList<ViabilityCondition> conditions)
    {
        if (conditions.Count == 0)
            return;

        csv.Append("\nResumo por condição,Composto,Concentração (µM),N,Média (%),Desvio (%)\n");
        foreach (ViabilityCondition condition in conditions)
        {
            csv.Append(',');
            csv.Append(Csv.Escape(condition.SampleId ?? "—"));
            csv.Append(',');
            csv.Append(condition.ConcentrationUm is { } concentration ? Format(concentration) : "—");
            csv.Append(',');
            csv.Append(condition.ReplicateCount.ToString(CultureInfo.InvariantCulture));
            csv.Append(',');
            csv.Append(Format(condition.MeanViabilityPct));
            csv.Append(',');
            csv.Append(condition.StdDevViabilityPct is { } sd ? Format(sd) : "—");
            csv.Append('\n');
        }
    }

    private static string Format(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed record ViabilityPayload(
        IReadOnlyList<ViabilityWell>? Wells,
        IReadOnlyList<ViabilityCondition>? Conditions);

    private sealed record ViabilityWell(decimal? ConcentrationUm, decimal ViabilityPct);

    private sealed record ViabilityCondition(
        string? SampleId,
        decimal? ConcentrationUm,
        int ReplicateCount,
        decimal MeanViabilityPct,
        decimal? StdDevViabilityPct);
}
