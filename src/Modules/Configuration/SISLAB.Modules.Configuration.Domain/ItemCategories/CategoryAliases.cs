using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Domain.ItemCategories;

/// <summary>
/// The alternative names (apelidos) a laboratory uses for an <see cref="ItemCategory"/> — e.g. a lab that
/// calls "Reagente" also "Reagentes" or "RGT". Modelled as an immutable value object with structural
/// equality (order-insensitive, case-insensitive, de-duplicated) so the category's set of aliases is a
/// single conceptual value rather than a loose list the caller could mutate.
/// </summary>
/// <remarks>
/// Aliases exist so imports and the UI can resolve a free-typed category name to the tenant's canonical
/// category without a hard match on the primary name. They are normalized (trimmed, blanks dropped, case-
/// insensitively de-duplicated) on construction, so the invariant "no blank/duplicate alias" holds by design.
/// </remarks>
public sealed class CategoryAliases : ValueObject
{
    private const int MaxAliasLength = 80;
    private const int MaxAliasCount = 20;

    /// <summary>The canonical empty set — a category with no configured aliases.</summary>
    public static readonly CategoryAliases None = new([]);

    private readonly IReadOnlyList<string> _values;

    private CategoryAliases(IReadOnlyList<string> values) => _values = values;

    /// <summary>The normalized aliases, in a stable (alphabetical, case-insensitive) order.</summary>
    public IReadOnlyList<string> Values => _values;

    /// <summary>
    /// Builds an alias set from raw input: trims each entry, drops blanks, de-duplicates case-insensitively
    /// and orders the result deterministically. A null input yields <see cref="None"/>.
    /// </summary>
    public static CategoryAliases From(IEnumerable<string>? aliases)
    {
        if (aliases is null)
            return None;

        List<string> normalized = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .DistinctBy(alias => alias.ToLowerInvariant())
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string alias in normalized)
        {
            if (alias.Length > MaxAliasLength)
                throw new DomainException(
                    $"A category alias cannot exceed {MaxAliasLength} characters.");
        }

        if (normalized.Count > MaxAliasCount)
            throw new DomainException($"A category cannot have more than {MaxAliasCount} aliases.");

        return new CategoryAliases(normalized);
    }

    /// <summary>Whether <paramref name="candidate"/> matches one of the aliases (case-insensitively).</summary>
    public bool Contains(string candidate)
        => !string.IsNullOrWhiteSpace(candidate)
           && _values.Any(alias => string.Equals(alias, candidate.Trim(), StringComparison.OrdinalIgnoreCase));

    protected override IEnumerable<object?> GetEqualityComponents()
        => _values.Select(alias => (object?)alias.ToLowerInvariant());
}
