using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// In vivo Prism CSV formatter for <c>von-frey-up-down@v1</c> (card [E11] #31). Lays the frozen per-(animal,
/// timepoint) 50% withdrawal thresholds out as a Prism grouped table: one column per timepoint and, within each
/// dose group, one replicate row per animal — the group × timepoint shape the operator pastes straight into a
/// Prism grouped analysis.
/// </summary>
/// <remarks>
/// It reads only the frozen snapshot JSON produced by <c>VonFreyUpDownCalculationStrategy</c> — never recomputes —
/// and the animal→group mapping supplied by the export query (sourced from the <c>Project</c> aggregate), so the
/// export reflects exactly what was signed off. Timepoints are ordered by label; groups are ordered by ascending
/// dose (the classic vehicle-first delineation). An animal with a threshold but no group assignment collapses into
/// a "Sem grupo" bucket so nothing is silently dropped.
/// </remarks>
internal sealed class VonFreyInVivoPrismFormatter : IInVivoPrismFormatter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public string FormulaCode => "von-frey-up-down@v1";

    /// <inheritdoc />
    public string Format(string resultJson, IReadOnlyList<AnimalGroupAssignment> animalGroups)
    {
        ArgumentNullException.ThrowIfNull(animalGroups);

        VonFreyPayload payload =
            JsonSerializer.Deserialize<VonFreyPayload>(resultJson, Options) ?? new VonFreyPayload(null);

        IReadOnlyList<AnimalThreshold> thresholds = payload.Thresholds ?? [];

        // Timepoint columns, ordered by label.
        IReadOnlyList<string> timepoints = thresholds
            .Select(threshold => threshold.Timepoint)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Animal → group assignment lookup (last wins on an accidental duplicate).
        Dictionary<Guid, AnimalGroupAssignment> byAnimal = animalGroups
            .GroupBy(assignment => assignment.AnimalId)
            .ToDictionary(group => group.Key, group => group.Last());

        // Threshold lookup by (animal, timepoint) for O(1) cell reads.
        Dictionary<(Guid Animal, string Timepoint), decimal> cell = thresholds
            .GroupBy(threshold => (threshold.AnimalId, threshold.Timepoint), TupleComparer)
            .ToDictionary(group => group.Key, group => group.Last().ThresholdGrams, TupleComparer);

        // Group the measured animals by their dose group, ordered by ascending dose then group name.
        var groups = thresholds
            .Select(threshold => threshold.AnimalId)
            .Distinct()
            .Select(animalId => (AnimalId: animalId, Assignment: byAnimal.GetValueOrDefault(animalId)))
            .GroupBy(entry => entry.Assignment?.GroupId)
            .Select(group => new
            {
                Header = group.First().Assignment is { } assignment
                    ? $"{assignment.GroupName} ({Format(assignment.DoseAmount)} {assignment.DoseUnit})"
                    : "Sem grupo",
                SortDose = group.First().Assignment?.DoseAmount ?? decimal.MaxValue,
                Animals = group
                    .OrderBy(entry => entry.Assignment?.AnimalIdentifier, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => entry.AnimalId)
                    .ToList(),
            })
            .OrderBy(group => group.SortDose)
            .ThenBy(group => group.Header, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var csv = new StringBuilder();

        // Header row: Grupo, <timepoint columns>.
        csv.Append("Grupo");
        foreach (string timepoint in timepoints)
        {
            csv.Append(',');
            csv.Append(Csv.Escape(timepoint));
        }
        csv.Append('\n');

        // One replicate row per animal, under its group; the first replicate row carries the group header.
        foreach (var group in groups)
        {
            int replicate = 0;
            foreach (Guid animalId in group.Animals)
            {
                csv.Append(Csv.Escape(replicate == 0 ? group.Header : string.Empty));
                foreach (string timepoint in timepoints)
                {
                    csv.Append(',');
                    if (cell.TryGetValue((animalId, timepoint), out decimal value))
                        csv.Append(Format(value));
                }
                csv.Append('\n');
                replicate++;
            }
        }

        return csv.ToString();
    }

    private static readonly IEqualityComparer<(Guid Animal, string Timepoint)> TupleComparer =
        new AnimalTimepointComparer();

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private sealed class AnimalTimepointComparer : IEqualityComparer<(Guid Animal, string Timepoint)>
    {
        public bool Equals((Guid Animal, string Timepoint) x, (Guid Animal, string Timepoint) y)
            => x.Animal == y.Animal
               && string.Equals(x.Timepoint, y.Timepoint, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid Animal, string Timepoint) obj)
            => HashCode.Combine(
                obj.Animal,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Timepoint));
    }

    private sealed record VonFreyPayload(IReadOnlyList<AnimalThreshold>? Thresholds);

    private sealed record AnimalThreshold(Guid AnimalId, string Timepoint, decimal ThresholdGrams);
}
