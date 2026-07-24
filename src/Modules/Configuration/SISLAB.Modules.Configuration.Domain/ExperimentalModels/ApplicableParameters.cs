using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// The set of physiological/behavioural parameters that apply to an experimental model (SISLAB-04): glicemia,
/// rotarod, peso in the ND example. An immutable value object with order-insensitive structural equality — it is
/// what SISLAB-02 will consult to decide, per model, which readings are offered and which are hidden ("glicemia
/// só no diabético").
/// </summary>
/// <remarks>
/// Modelled as a normalized set of parameter <b>codes</b> (trimmed, blanks dropped, case-insensitively
/// de-duplicated, alphabetized for a stable representation). The codes are cadastered per model — the current
/// lab's "glicemia/rotarod/peso" are just one instance, never a code constant. An empty set is allowed: a model
/// may apply no recurring physiological parameter.
/// </remarks>
public sealed class ApplicableParameters : ValueObject
{
    private const int MaxCodeLength = 60;
    private const int MaxCount = 50;

    /// <summary>The canonical empty set — a model with no applicable recurring parameter.</summary>
    public static readonly ApplicableParameters None = new([]);

    private readonly IReadOnlyList<string> _codes;

    private ApplicableParameters(IReadOnlyList<string> codes) => _codes = codes;

    /// <summary>The normalized parameter codes, in a stable (alphabetical, case-insensitive) order.</summary>
    public IReadOnlyList<string> Codes => _codes;

    /// <summary>
    /// Builds the set from raw codes: trims each, drops blanks, de-duplicates case-insensitively and orders the
    /// result deterministically. A null input yields <see cref="None"/>.
    /// </summary>
    public static ApplicableParameters From(IEnumerable<string>? codes)
    {
        if (codes is null)
            return None;

        List<string> normalized = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .DistinctBy(code => code.ToLowerInvariant())
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string code in normalized)
        {
            if (code.Length > MaxCodeLength)
                throw new DomainException($"A parameter code cannot exceed {MaxCodeLength} characters.");
        }

        if (normalized.Count > MaxCount)
            throw new DomainException($"An experimental model cannot define more than {MaxCount} applicable parameters.");

        return new ApplicableParameters(normalized);
    }

    /// <summary>Whether <paramref name="code"/> is one of the applicable parameters (case-insensitively).</summary>
    public bool Applies(string code)
        => !string.IsNullOrWhiteSpace(code)
           && _codes.Any(existing => string.Equals(existing, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
        => _codes.Select(code => (object?)code.ToLowerInvariant());
}
