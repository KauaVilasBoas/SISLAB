using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Domain.Plates;

/// <summary>
/// The 8×12 microplate of a cell-viability experiment (decision card #68). It owns the collection of
/// <see cref="Well"/>s and the invariants over them: a well coordinate is unique, every coordinate is within
/// the fixed 8 rows (A–H) × 12 columns (1–12) grid, and the plate is redesigned as a whole rather than mutated
/// well-by-well from outside.
/// </summary>
/// <remarks>
/// The plate is an owned part of the <c>ViabilidadeCelularExperiment</c> aggregate — it has no identity of its
/// own and is only reached through the experiment. Its wells live in the <c>experiments.wells</c> table
/// (decision: a separate table, not a JSON blob — see <see cref="Well"/>). The fixed <see cref="Rows"/> /
/// <see cref="Columns"/> dimensions are domain constants, not persisted columns.
/// </remarks>
public sealed class Plate
{
    /// <summary>Number of rows on the plate (A–H).</summary>
    public const int Rows = 8;

    /// <summary>Number of columns on the plate (1–12).</summary>
    public const int Columns = 12;

    private readonly List<Well> _wells = [];

    /// <summary>Creates an empty plate (no wells designed yet).</summary>
    public Plate() { }

    /// <summary>The wells currently designed on the plate.</summary>
    public IReadOnlyList<Well> Wells => _wells.AsReadOnly();

    /// <summary>True once at least one well has been designed.</summary>
    public bool IsDesigned => _wells.Count > 0;

    /// <summary>
    /// Replaces the entire plate layout with the supplied wells, enforcing coordinate uniqueness. Designing
    /// the plate as a whole (rather than well-by-well) matches how the operator lays out a plate in one go and
    /// keeps the "no duplicate coordinate" invariant trivial to guarantee.
    /// </summary>
    public void Design(IEnumerable<Well> wells)
    {
        ArgumentNullException.ThrowIfNull(wells);

        var designed = wells.ToList();

        var duplicate = designed
            .GroupBy(well => well.Coordinate)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new DomainException($"Well coordinate '{duplicate.Key}' is repeated in the plate design.");

        if (designed.Count == 0)
            throw new DomainException("A plate design must contain at least one well.");

        _wells.Clear();
        _wells.AddRange(designed);
    }

    /// <summary>
    /// Applies the reader's absorbance for a single well identified by its coordinate. An unknown coordinate
    /// (a value for a well that was not designed) is rejected — the import must match the plate layout.
    /// </summary>
    public void RecordAbsorbance(string coordinate, decimal rawAbsorbance)
    {
        Well well = _wells.FirstOrDefault(w =>
                        string.Equals(w.Coordinate, coordinate, StringComparison.OrdinalIgnoreCase))
                    ?? throw new DomainException(
                        $"Cannot import a reading for well '{coordinate}': it is not part of the plate design.");

        well.RecordAbsorbance(rawAbsorbance);
    }

    /// <summary>True once every designed well has an imported absorbance (ready to calculate).</summary>
    public bool HasCompleteReading => _wells.Count > 0 && _wells.All(well => well.HasReading);
}
