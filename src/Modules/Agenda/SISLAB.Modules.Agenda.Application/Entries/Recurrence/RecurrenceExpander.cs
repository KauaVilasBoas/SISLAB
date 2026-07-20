using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace SISLAB.Modules.Agenda.Application.Entries.Recurrence;

/// <summary>
/// A single materialised occurrence of an agenda entry — either a one-off entry (the entry itself) or one
/// instance of a recurring series expanded within a window (cards [E10.4] #4, reused by [E10.9]/[E10.11]).
/// </summary>
/// <param name="StartUtc">The occurrence start in UTC.</param>
/// <param name="EndUtc">The occurrence end in UTC.</param>
/// <param name="OccurrenceDate">The occurrence's date (its EXDATE key), used to cancel/identify a single instance.</param>
public readonly record struct EntryOccurrence(DateTime StartUtc, DateTime EndUtc, DateOnly OccurrenceDate);

/// <summary>
/// Expands an agenda entry into its concrete occurrences within a date window using Ical.Net for the RFC 5545
/// heavy lifting (RRULE + EXDATE), so the module never hand-rolls a recurrence engine. Shared by every read
/// path that needs the actual instances: the calendar projection, conflict detection and room occupancy.
/// </summary>
/// <remarks>
/// <para>
/// A one-off entry (no rule) yields exactly its own single interval when it overlaps the window. A recurring
/// entry is fed to a transient <see cref="CalendarEvent"/> whose <c>RRULE</c>/<c>EXDATE</c> mirror the stored
/// values, then <see cref="CalendarEvent.GetOccurrences(System.DateTime, System.DateTime)"/> enumerates the
/// instances in the window — excluded dates drop out automatically. All times are kept in UTC.
/// </para>
/// </remarks>
public sealed class RecurrenceExpander
{
    /// <summary>
    /// Returns the occurrences of the entry described by the arguments that intersect the inclusive window
    /// [<paramref name="windowStartUtc"/>, <paramref name="windowEndUtc"/>]. Ordered by start time.
    /// </summary>
    public IReadOnlyList<EntryOccurrence> Expand(
        DateTime entryStartUtc,
        DateTime entryEndUtc,
        string? recurrenceRule,
        IReadOnlyCollection<DateOnly> excludedDates,
        DateTime windowStartUtc,
        DateTime windowEndUtc)
    {
        if (string.IsNullOrWhiteSpace(recurrenceRule))
            return ExpandSingle(entryStartUtc, entryEndUtc, windowStartUtc, windowEndUtc);

        return ExpandRecurring(
            entryStartUtc, entryEndUtc, recurrenceRule, excludedDates, windowStartUtc, windowEndUtc);
    }

    private static IReadOnlyList<EntryOccurrence> ExpandSingle(
        DateTime entryStartUtc, DateTime entryEndUtc, DateTime windowStartUtc, DateTime windowEndUtc)
    {
        // A one-off contributes iff its interval overlaps the window (half-open on the far edges).
        bool overlaps = entryStartUtc <= windowEndUtc && entryEndUtc >= windowStartUtc;
        return overlaps
            ? [new EntryOccurrence(entryStartUtc, entryEndUtc, DateOnly.FromDateTime(entryStartUtc))]
            : [];
    }

    private static IReadOnlyList<EntryOccurrence> ExpandRecurring(
        DateTime entryStartUtc,
        DateTime entryEndUtc,
        string recurrenceRule,
        IReadOnlyCollection<DateOnly> excludedDates,
        DateTime windowStartUtc,
        DateTime windowEndUtc)
    {
        TimeSpan duration = entryEndUtc - entryStartUtc;

        var calendarEvent = new CalendarEvent
        {
            Start = new CalDateTime(DateTime.SpecifyKind(entryStartUtc, DateTimeKind.Utc)),
            Duration = duration,
        };
        calendarEvent.RecurrenceRules.Add(new RecurrencePattern(recurrenceRule));

        foreach (DateOnly excluded in excludedDates)
        {
            var exdate = new CalDateTime(
                excluded.ToDateTime(TimeOnly.FromDateTime(entryStartUtc), DateTimeKind.Utc));
            calendarEvent.ExceptionDates.Add(new PeriodList { exdate });
        }

        // GetOccurrences is inclusive of the endpoints; keep it in UTC so start/end never shift by a zone.
        IEnumerable<Occurrence> occurrences = calendarEvent.GetOccurrences(windowStartUtc, windowEndUtc);

        var result = new List<EntryOccurrence>();
        foreach (Occurrence occurrence in occurrences)
        {
            DateTime startUtc = DateTime.SpecifyKind(occurrence.Period.StartTime.Value, DateTimeKind.Utc);
            DateTime endUtc = startUtc + duration;
            result.Add(new EntryOccurrence(startUtc, endUtc, DateOnly.FromDateTime(startUtc)));
        }

        return result
            .OrderBy(occurrence => occurrence.StartUtc)
            .ToList();
    }
}
