using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Domain.Scheduling;

/// <summary>
/// The configurable duty roster that rotates responsibles across the days of a generated experiment schedule
/// (SISLAB-10) — the spreadsheet's "Vic e Dai" day-on/day-off alternation, expressed generically. An immutable
/// value object owning an <b>ordered</b> list of responsibles (by value, as <see cref="Guid"/> ids) and a
/// <see cref="DaysPerShift"/> cadence: consecutive scheduled days are grouped into shifts of that length and each
/// shift is handed to the next responsible, wrapping round-robin.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing lab-specific is fixed.</b> Neither the people nor the cadence are code constants: a lab supplies its
/// own ordered responsible list and its own <see cref="DaysPerShift"/>. Two responsibles with
/// <see cref="DaysPerShift"/> = 1 reproduce the Vic/Dai "dia sim, dia não" alternation; a longer list or a wider
/// shift is just another configuration of the same rule.
/// </para>
/// <para>
/// <b>Deterministic by day index.</b> <see cref="ResponsibleForDay"/> takes the zero-based index of a scheduled
/// day (its position in the ordered schedule, not a calendar date) and returns the responsible on duty, so the
/// rotation is reproducible and testable without any clock or persistence.
/// </para>
/// </remarks>
public sealed class ResponsibleRoster : ValueObject
{
    private readonly IReadOnlyList<Guid> _responsibles;

    private ResponsibleRoster(IReadOnlyList<Guid> responsibles, int daysPerShift)
    {
        _responsibles = responsibles;
        DaysPerShift = daysPerShift;
    }

    /// <summary>The responsibles in rotation order (by value); the rotation cycles back to the first after the last.</summary>
    public IReadOnlyList<Guid> Responsibles => _responsibles;

    /// <summary>How many consecutive scheduled days one responsible covers before the duty passes to the next (≥ 1).</summary>
    public int DaysPerShift { get; }

    /// <summary>
    /// Builds a roster from an ordered responsible list and a shift length. Requires at least one responsible and a
    /// shift length of at least one day; rejects an empty guid (an unassigned responsible).
    /// </summary>
    public static ResponsibleRoster Of(IEnumerable<Guid> responsibles, int daysPerShift = 1)
    {
        ArgumentNullException.ThrowIfNull(responsibles);

        List<Guid> ordered = responsibles.ToList();

        if (ordered.Count == 0)
            throw new DomainException("A responsible roster requires at least one responsible.");

        if (ordered.Any(id => id == Guid.Empty))
            throw new DomainException("A responsible roster cannot contain an empty responsible id.");

        if (daysPerShift < 1)
            throw new DomainException("A roster shift must cover at least one day.");

        return new ResponsibleRoster(ordered, daysPerShift);
    }

    /// <summary>
    /// The responsible on duty for the scheduled day at <paramref name="dayIndex"/> (zero-based position in the
    /// schedule). The day is mapped to its shift (<c>dayIndex / DaysPerShift</c>) and the shift to a responsible
    /// round-robin (<c>shift % Responsibles.Count</c>). Rejects a negative index.
    /// </summary>
    public Guid ResponsibleForDay(int dayIndex)
    {
        if (dayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(dayIndex), dayIndex, "A day index cannot be negative.");

        int shift = dayIndex / DaysPerShift;
        return _responsibles[shift % _responsibles.Count];
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DaysPerShift;
        foreach (Guid responsible in _responsibles)
            yield return responsible;
    }
}
