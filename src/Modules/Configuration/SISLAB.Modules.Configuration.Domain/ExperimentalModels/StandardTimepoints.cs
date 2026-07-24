using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// The ordered set of default timepoint labels an experimental model measures at (SISLAB-04): basal, pós-indução,
/// 7/15/21 dias, 28° dia in the ND example. An immutable value object with structural equality — a single
/// conceptual value the aggregate holds, not a loose list the caller could mutate.
/// </summary>
/// <remarks>
/// <b>Order is meaningful.</b> Unlike a category alias set, the reading order matters (basal comes before
/// pós-indução, day 7 before day 28), so insertion order is preserved (blanks dropped, case-insensitively
/// de-duplicated) rather than alphabetized. At least one timepoint is required — a model with no timepoint would
/// drive no readouts. The concrete labels are cadastered per model; nothing here is a code constant.
/// </remarks>
public sealed class StandardTimepoints : ValueObject
{
    private const int MaxLabelLength = 60;
    private const int MaxCount = 50;

    private readonly IReadOnlyList<string> _labels;

    private StandardTimepoints(IReadOnlyList<string> labels) => _labels = labels;

    /// <summary>The normalized timepoint labels, in the (meaningful) reading order they were supplied in.</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>
    /// Builds the timepoint set from raw labels: trims each, drops blanks, de-duplicates case-insensitively while
    /// keeping the first occurrence's order. Requires at least one non-blank label.
    /// </summary>
    public static StandardTimepoints From(IEnumerable<string>? labels)
    {
        List<string> normalized = Normalize(labels);

        if (normalized.Count == 0)
            throw new DomainException("An experimental model must define at least one standard timepoint.");

        if (normalized.Count > MaxCount)
            throw new DomainException($"An experimental model cannot define more than {MaxCount} timepoints.");

        return new StandardTimepoints(normalized);
    }

    private static List<string> Normalize(IEnumerable<string>? labels)
    {
        if (labels is null)
            return [];

        List<string> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string? raw in labels)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string trimmed = raw.Trim();

            if (trimmed.Length > MaxLabelLength)
                throw new DomainException($"A timepoint label cannot exceed {MaxLabelLength} characters.");

            if (seen.Add(trimmed))
                result.Add(trimmed);
        }

        return result;
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
        => _labels.Select(label => (object?)label.ToLowerInvariant());
}
