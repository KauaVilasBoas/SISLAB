using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Domain.Scheduling;

/// <summary>
/// Pure domain service that generates an experiment's schedule (SISLAB-10) from an induction protocol, a set of
/// treatment/timepoint day offsets and a start date, then assigns each resulting day a responsible from a
/// <see cref="ResponsibleRoster"/>. It is the heart of the card: the calendar the lab keeps by hand in the in vivo
/// spreadsheet (1st/2nd induction, treatment days, the 28th-day readout, "Vic e Dai" rotation) derived instead from
/// the model's cadence.
/// </summary>
/// <remarks>
/// <para>
/// <b>All cadence comes from the model.</b> The number of inductions and their spacing are the protocol's
/// (<paramref name="administrations"/> / <paramref name="intervalDays"/>); the treatment and timepoint days are
/// supplied offsets derived from the model. Nothing about the current lab (no fixed 28-day, no fixed Vic/Dai) is a
/// code constant — change the model and the schedule changes.
/// </para>
/// <para>
/// <b>Deterministic and side-effect free.</b> Given the same inputs it always yields the same ordered list, so it is
/// exhaustively unit-testable without a clock, database or the Agenda module. Days are emitted in chronological order
/// (ties broken by kind: induction, then treatment, then timepoint) and the roster is applied over that order, so a
/// responsible covers a whole day regardless of how many activities fall on it.
/// </para>
/// </remarks>
public sealed class ExperimentScheduleGenerator
{
    /// <summary>
    /// Generates the ordered schedule.
    /// </summary>
    /// <param name="startDate">Day 0 of the schedule — the first induction day.</param>
    /// <param name="administrations">Number of induction administrations (≥ 1), from the protocol.</param>
    /// <param name="intervalDays">Days between consecutive inductions (≥ 0), from the protocol.</param>
    /// <param name="treatmentDayOffsets">Day offsets (from the start) of treatment/administration days, from the model.</param>
    /// <param name="timepoints">The model's timepoints paired with their day offsets (from the start).</param>
    /// <param name="roster">The configurable duty roster that assigns a responsible to each scheduled day.</param>
    /// <returns>The schedule entries in chronological order, each with its roster-assigned responsible.</returns>
    public IReadOnlyList<ScheduledActivity> Generate(
        DateOnly startDate,
        int administrations,
        int intervalDays,
        IReadOnlyList<int> treatmentDayOffsets,
        IReadOnlyList<ScheduledTimepoint> timepoints,
        ResponsibleRoster roster)
    {
        ArgumentNullException.ThrowIfNull(treatmentDayOffsets);
        ArgumentNullException.ThrowIfNull(timepoints);
        ArgumentNullException.ThrowIfNull(roster);

        if (administrations < 1)
            throw new DomainException("A schedule requires at least one induction administration.");

        if (intervalDays < 0)
            throw new DomainException("The interval between inductions cannot be negative.");

        if (treatmentDayOffsets.Any(offset => offset < 0) || timepoints.Any(timepoint => timepoint.DayOffset < 0))
            throw new DomainException("A schedule day offset cannot be negative.");

        // Collect every (offset, kind, label) the protocol/model prescribes, keyed by the day offset from the start.
        var draft = new List<DraftActivity>();

        for (int i = 0; i < administrations; i++)
        {
            int offset = i * intervalDays;
            string label = administrations == 1 ? "Indução" : $"{i + 1}ª indução";
            draft.Add(new DraftActivity(offset, ScheduleActivityKind.Induction, label));
        }

        foreach (int offset in treatmentDayOffsets)
            draft.Add(new DraftActivity(offset, ScheduleActivityKind.Treatment, $"Tratamento — dia {offset}"));

        foreach (ScheduledTimepoint timepoint in timepoints)
            draft.Add(new DraftActivity(timepoint.DayOffset, ScheduleActivityKind.Timepoint, timepoint.Label));

        // Chronological order; on the same day, induction before treatment before timepoint (enum order) so the day's
        // entries read in a natural sequence. The roster is applied over the distinct days, so one responsible owns a
        // whole day even when several activities share it.
        List<DraftActivity> ordered = draft
            .OrderBy(activity => activity.Offset)
            .ThenBy(activity => (int)activity.Kind)
            .ToList();

        List<int> distinctDays = ordered
            .Select(activity => activity.Offset)
            .Distinct()
            .OrderBy(offset => offset)
            .ToList();

        // Map each distinct day to its zero-based position, so the roster rotates per day, not per activity.
        Dictionary<int, int> dayIndexByOffset = distinctDays
            .Select((offset, index) => (offset, index))
            .ToDictionary(pair => pair.offset, pair => pair.index);

        return ordered
            .Select(activity => new ScheduledActivity(
                startDate.AddDays(activity.Offset),
                activity.Kind,
                activity.Label,
                roster.ResponsibleForDay(dayIndexByOffset[activity.Offset])))
            .ToList();
    }

    // Internal working record before a start date and responsible are applied.
    private readonly record struct DraftActivity(int Offset, ScheduleActivityKind Kind, string Label);
}
