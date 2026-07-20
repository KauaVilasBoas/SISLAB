using SISLAB.Modules.Agenda.Application.Entries.Recurrence;

namespace SISLAB.Modules.Agenda.Tests.Application;

/// <summary>
/// Tests for the Ical.Net-backed <see cref="RecurrenceExpander"/> (card [E10.4] #4): one-off overlap, weekly and
/// daily expansion within a window, EXDATE exclusion and UNTIL truncation.
/// </summary>
public sealed class RecurrenceExpanderTests
{
    private readonly RecurrenceExpander _expander = new();

    private static DateTime Utc(int y, int m, int d, int h = 9) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OneOff_within_window_yields_single_occurrence()
    {
        IReadOnlyList<EntryOccurrence> result = _expander.Expand(
            Utc(2026, 8, 10), Utc(2026, 8, 10, 10), recurrenceRule: null, excludedDates: [],
            windowStartUtc: Utc(2026, 8, 1, 0), windowEndUtc: Utc(2026, 8, 31, 23));

        EntryOccurrence only = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 8, 10), only.OccurrenceDate);
    }

    [Fact]
    public void OneOff_outside_window_yields_nothing()
    {
        IReadOnlyList<EntryOccurrence> result = _expander.Expand(
            Utc(2026, 7, 10), Utc(2026, 7, 10, 10), recurrenceRule: null, excludedDates: [],
            windowStartUtc: Utc(2026, 8, 1, 0), windowEndUtc: Utc(2026, 8, 31, 23));

        Assert.Empty(result);
    }

    [Fact]
    public void Daily_series_expands_each_day_in_window()
    {
        IReadOnlyList<EntryOccurrence> result = _expander.Expand(
            Utc(2026, 8, 1), Utc(2026, 8, 1, 10), "FREQ=DAILY", excludedDates: [],
            windowStartUtc: Utc(2026, 8, 1, 0), windowEndUtc: Utc(2026, 8, 5, 23));

        Assert.Equal(5, result.Count);
        Assert.Equal(new DateOnly(2026, 8, 1), result[0].OccurrenceDate);
        Assert.Equal(new DateOnly(2026, 8, 5), result[^1].OccurrenceDate);
    }

    [Fact]
    public void Weekly_series_expands_only_matching_weekdays()
    {
        // Fridays in August 2026: 7, 14, 21, 28.
        IReadOnlyList<EntryOccurrence> result = _expander.Expand(
            Utc(2026, 8, 7), Utc(2026, 8, 7, 10), "FREQ=WEEKLY;BYDAY=FR", excludedDates: [],
            windowStartUtc: Utc(2026, 8, 1, 0), windowEndUtc: Utc(2026, 8, 31, 23));

        Assert.Equal(4, result.Count);
        Assert.All(result, occurrence =>
            Assert.Equal(DayOfWeek.Friday, occurrence.StartUtc.DayOfWeek));
    }

    [Fact]
    public void Excluded_date_is_dropped_from_expansion()
    {
        IReadOnlyList<EntryOccurrence> result = _expander.Expand(
            Utc(2026, 8, 1), Utc(2026, 8, 1, 10), "FREQ=DAILY",
            excludedDates: [new DateOnly(2026, 8, 3)],
            windowStartUtc: Utc(2026, 8, 1, 0), windowEndUtc: Utc(2026, 8, 5, 23));

        Assert.Equal(4, result.Count);
        Assert.DoesNotContain(result, occurrence => occurrence.OccurrenceDate == new DateOnly(2026, 8, 3));
    }

    [Fact]
    public void Until_truncates_the_series()
    {
        IReadOnlyList<EntryOccurrence> result = _expander.Expand(
            Utc(2026, 8, 1), Utc(2026, 8, 1, 10), "FREQ=DAILY;UNTIL=20260803T235959Z", excludedDates: [],
            windowStartUtc: Utc(2026, 8, 1, 0), windowEndUtc: Utc(2026, 8, 31, 23));

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateOnly(2026, 8, 3), result[^1].OccurrenceDate);
    }

    [Fact]
    public void Occurrence_preserves_the_entry_duration()
    {
        IReadOnlyList<EntryOccurrence> result = _expander.Expand(
            Utc(2026, 8, 1, 9), Utc(2026, 8, 1, 11), "FREQ=DAILY", excludedDates: [],
            windowStartUtc: Utc(2026, 8, 1, 0), windowEndUtc: Utc(2026, 8, 2, 23));

        Assert.All(result, occurrence =>
            Assert.Equal(TimeSpan.FromHours(2), occurrence.EndUtc - occurrence.StartUtc));
    }
}
