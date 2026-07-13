using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Domain.ReferenceRanges;

/// <summary>
/// The inclusive numeric bounds of a <see cref="ReferenceRange"/> — the healthy interval an analyte's
/// result is compared against. An immutable value object with the single invariant that the minimum never
/// exceeds the maximum, so an inverted interval can never exist.
/// </summary>
/// <remarks>
/// An open-ended bound is expressed with <see langword="null"/> (e.g. "&lt;= 5.0" has a null minimum,
/// "&gt;= 12" a null maximum). When both bounds are present the invariant <c>min &lt;= max</c> is enforced;
/// at least one bound must be present, since a range with neither would classify nothing.
/// </remarks>
public sealed class RangeBounds : ValueObject
{
    private RangeBounds(decimal? minimum, decimal? maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Inclusive lower bound, or <see langword="null"/> for an open lower end.</summary>
    public decimal? Minimum { get; }

    /// <summary>Inclusive upper bound, or <see langword="null"/> for an open upper end.</summary>
    public decimal? Maximum { get; }

    /// <summary>
    /// Builds the bounds, enforcing that at least one is present and, when both are, that
    /// <paramref name="minimum"/> does not exceed <paramref name="maximum"/>.
    /// </summary>
    public static RangeBounds Of(decimal? minimum, decimal? maximum)
    {
        if (minimum is null && maximum is null)
            throw new DomainException("A reference range must define at least a minimum or a maximum.");

        if (minimum is { } min && maximum is { } max && min > max)
            throw new DomainException(
                $"The reference range minimum ({min}) cannot be greater than the maximum ({max}).");

        return new RangeBounds(minimum, maximum);
    }

    /// <summary>Whether <paramref name="value"/> falls within the (inclusive) bounds.</summary>
    public bool Contains(decimal value)
        => (Minimum is null || value >= Minimum) && (Maximum is null || value <= Maximum);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Minimum;
        yield return Maximum;
    }
}
