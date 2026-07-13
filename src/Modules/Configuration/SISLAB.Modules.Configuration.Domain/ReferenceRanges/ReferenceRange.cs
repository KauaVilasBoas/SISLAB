using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.ReferenceRanges;

/// <summary>
/// A per-tenant reference range (card [E12] #76): the healthy interval for a laboratory analyte, scoped by
/// species/strain — e.g. the hemoglobin reference for a given mouse strain. The future Experiments module
/// will use it to flag results outside the expected interval instead of hardcoding thresholds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identity.</b> A range is identified within a tenant by <c>(analyte, species)</c> — the same analyte
/// has different healthy intervals per species/strain, so both are part of the natural key (a unique index
/// enforces it). The optional <see cref="Unit"/> records the measurement unit the bounds are expressed in.
/// </para>
/// <para>
/// <b>Invariant in a value object.</b> The numeric interval lives in <see cref="RangeBounds"/>, which owns
/// the "min ≤ max, at least one bound" rule, so the aggregate cannot hold an inverted or empty range.
/// </para>
/// </remarks>
public sealed class ReferenceRange : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxAnalyteLength = 120;
    private const int MaxSpeciesLength = 120;
    private const int MaxUnitLength = 20;

    // Parameterless constructor for EF Core materialization.
    private ReferenceRange() : base(Guid.Empty) { }

    private ReferenceRange(Guid id, string analyte, string species, RangeBounds bounds, string? unit) : base(id)
    {
        Analyte = analyte;
        Species = species;
        Bounds = bounds;
        Unit = unit;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>The measured analyte (e.g. "Hemoglobina").</summary>
    public string Analyte { get; private set; } = default!;

    /// <summary>The species/strain the range applies to (e.g. "Camundongo C57BL/6").</summary>
    public string Species { get; private set; } = default!;

    /// <summary>The healthy interval, owning the min/max invariant.</summary>
    public RangeBounds Bounds { get; private set; } = default!;

    /// <summary>Measurement unit the bounds are expressed in (e.g. "g/dL"), or <see langword="null"/>.</summary>
    public string? Unit { get; private set; }

    /// <summary>Creates a reference range for an analyte/species with validated bounds.</summary>
    public static ReferenceRange Create(
        string analyte,
        string species,
        decimal? minimum,
        decimal? maximum,
        string? unit = null)
        => new(
            Guid.NewGuid(),
            NormalizeText(analyte, MaxAnalyteLength, nameof(analyte)),
            NormalizeText(species, MaxSpeciesLength, nameof(species)),
            RangeBounds.Of(minimum, maximum),
            NormalizeOptional(unit, MaxUnitLength, nameof(unit)));

    /// <summary>Replaces the healthy interval, keeping the (analyte, species) identity.</summary>
    public void ChangeBounds(decimal? minimum, decimal? maximum) => Bounds = RangeBounds.Of(minimum, maximum);

    /// <summary>Sets or clears the measurement unit.</summary>
    public void ChangeUnit(string? unit) => Unit = NormalizeOptional(unit, MaxUnitLength, nameof(unit));

    private static string NormalizeText(string value, int maxLength, string parameterName)
    {
        Guard.AgainstNullOrWhiteSpace(value, parameterName);
        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
