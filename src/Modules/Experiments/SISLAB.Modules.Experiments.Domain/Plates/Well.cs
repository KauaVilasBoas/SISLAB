using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Domain.Plates;

/// <summary>
/// A single well of a <see cref="Plate"/> (decision card #68 — plate 8×12). Identified physically by its
/// <see cref="Row"/> (A–H) and <see cref="Column"/> (1–12) coordinate; carries the <see cref="Role"/> the
/// well plays in the assay, an optional test <see cref="ConcentrationUm"/> (µM) and free-text
/// <see cref="SampleId"/>, and — once the plate reader is imported — its <see cref="RawAbsorbance"/>.
/// </summary>
/// <remarks>
/// A well is a child entity of the plate/experiment aggregate: it is never mutated from outside, only through
/// the aggregate's methods (<c>DesignWell</c> / <c>RecordAbsorbance</c>). It is persisted in its own
/// <c>experiments.wells</c> table (decision: a separate table, not JSON — 96 wells per plate would be an
/// unwieldy JSON blob). The surrogate <see cref="Entity{TId}.Id"/> is the table key; (row, column) is the
/// natural coordinate, kept unique per plate by the aggregate.
/// </remarks>
public sealed class Well : Entity<Guid>
{
    private const int MinColumn = 1;
    private const int MaxColumn = 12;
    private const char MinRow = 'A';
    private const char MaxRow = 'H';

    // Parameterless constructor for EF Core materialization.
    private Well() : base(Guid.Empty) { }

    private const int MaxExclusionReasonLength = 500;

    private Well(Guid id, char row, int column, WellRole role, decimal? concentrationUm, string? sampleId)
        : base(id)
    {
        Row = row;
        Column = column;
        Role = role;
        ConcentrationUm = concentrationUm;
        SampleId = sampleId;
    }

    /// <summary>Plate row, A–H (8 rows).</summary>
    public char Row { get; private set; }

    /// <summary>Plate column, 1–12 (12 columns).</summary>
    public int Column { get; private set; }

    /// <summary>Role the well plays in the assay — drives the viability calculation.</summary>
    public WellRole Role { get; private set; }

    /// <summary>Test concentration in µM, when the well is a curve/sample point; otherwise null.</summary>
    public decimal? ConcentrationUm { get; private set; }

    /// <summary>Free-text sample identifier (e.g. a partner compound code); optional.</summary>
    public string? SampleId { get; private set; }

    /// <summary>Raw absorbance read by the plate reader; null until the reading is imported.</summary>
    public decimal? RawAbsorbance { get; private set; }

    /// <summary>True once the plate reader value has been imported for this well.</summary>
    public bool HasReading => RawAbsorbance.HasValue;

    /// <summary>
    /// True when the operator has marked this replicate as an excluded outlier (SISLAB-06). An excluded well is
    /// ignored by the calculation — it never enters a control/blank mean, the calibration curve or the per-well
    /// results — but it is kept in the design (with its reason/author) for traceability.
    /// </summary>
    public bool IsExcluded { get; private set; }

    /// <summary>The operator's reason for excluding this well as an outlier, or null when the well is included.</summary>
    public string? ExclusionReason { get; private set; }

    /// <summary>The actor who excluded this well (audit claim), or null when the well is included.</summary>
    public string? ExcludedBy { get; private set; }

    /// <summary>
    /// True when this well takes part in the calculation: it has a reading and has not been excluded. The
    /// strategies use it in place of <see cref="HasReading"/> so excluded replicates drop out of every mean,
    /// curve fit and result line.
    /// </summary>
    public bool CountsTowardCalculation => HasReading && !IsExcluded;

    /// <summary>
    /// Creates a well at the given coordinate with its assay role and optional metadata. Guards the
    /// coordinate against the 8×12 plate bounds so an out-of-range well can never exist.
    /// </summary>
    public static Well Create(char row, int column, WellRole role, decimal? concentrationUm, string? sampleId)
    {
        char normalizedRow = char.ToUpperInvariant(row);

        if (normalizedRow is < MinRow or > MaxRow)
            throw new DomainException(
                $"Well row '{row}' is out of range. Expected {MinRow}–{MaxRow}.");

        if (column is < MinColumn or > MaxColumn)
            throw new DomainException(
                $"Well column '{column}' is out of range. Expected {MinColumn}–{MaxColumn}.");

        string? normalizedSampleId = string.IsNullOrWhiteSpace(sampleId) ? null : sampleId.Trim();

        return new Well(Guid.NewGuid(), normalizedRow, column, role, NormalizeConcentration(concentrationUm), normalizedSampleId);
    }

    /// <summary>
    /// Sets (or clears) this well's test concentration in µM (SISLAB-05), used when a computed serial-dilution
    /// scheme is applied to the plate. Mutated only through the aggregate (<see cref="Plate"/>/<c>PlateExperiment</c>),
    /// which decides which wells a series covers. A negative value is normalised to null (no concentration), matching
    /// the creation-time rule.
    /// </summary>
    internal void AssignConcentration(decimal? concentrationUm)
        => ConcentrationUm = NormalizeConcentration(concentrationUm);

    /// <summary>Records the plate reader's raw absorbance for this well. Must be non-negative.</summary>
    public void RecordAbsorbance(decimal rawAbsorbance)
    {
        if (rawAbsorbance < 0)
            throw new DomainException(
                $"Absorbance for well {Coordinate} cannot be negative. Received: {rawAbsorbance}.");

        RawAbsorbance = rawAbsorbance;
    }

    /// <summary>
    /// Marks this well as an excluded outlier with the operator's <paramref name="reason"/> and the acting
    /// <paramref name="actor"/> (SISLAB-06). Re-excluding an already-excluded well just refreshes the reason/author.
    /// The "cannot exclude after the calculation is frozen" invariant is enforced by the aggregate
    /// (<c>PlateExperiment</c>), which owns the well and knows whether the snapshot exists.
    /// </summary>
    public void Exclude(string reason, string actor)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException($"An exclusion reason is required to exclude well {Coordinate}.");

        if (string.IsNullOrWhiteSpace(actor))
            throw new DomainException($"An actor is required to exclude well {Coordinate}.");

        string trimmedReason = reason.Trim();
        if (trimmedReason.Length > MaxExclusionReasonLength)
            throw new DomainException(
                $"The exclusion reason for well {Coordinate} cannot exceed {MaxExclusionReasonLength} characters.");

        IsExcluded = true;
        ExclusionReason = trimmedReason;
        ExcludedBy = actor.Trim();
    }

    /// <summary>Clears the exclusion, bringing the well back into the calculation (SISLAB-06).</summary>
    public void Include()
    {
        IsExcluded = false;
        ExclusionReason = null;
        ExcludedBy = null;
    }

    /// <summary>The well coordinate as the canonical label, e.g. "A1", "H12".</summary>
    public string Coordinate => $"{Row}{Column}";

    private static decimal? NormalizeConcentration(decimal? concentrationUm)
        => concentrationUm is { } value && value < 0 ? null : concentrationUm;
}
