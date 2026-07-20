using SISLAB.Modules.Agenda.Application.Subscriptions.Queries;

namespace SISLAB.Modules.Agenda.Tests.Application;

/// <summary>
/// Tests for the .ics writer (card [E10.10]): each entry becomes one VEVENT, recurring entries keep their
/// RRULE/EXDATE verbatim (the client expands, we never materialise occurrences), and all-day entries are emitted
/// as DATE values.
/// </summary>
public sealed class IcalFeedBuilderTests
{
    private readonly IcalFeedBuilder _builder = new();

    private static IcalEntry OneOff(string title, DateTime start, DateTime end) =>
        new(Guid.NewGuid(), title, "desc", start, end, IsAllDay: false,
            RecurrenceRule: null, ExcludedDates: []);

    [Fact]
    public void Empty_feed_is_a_valid_vcalendar_with_no_events()
    {
        string ics = _builder.Build([]);

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("VERSION:2.0", ics);
        Assert.Contains("END:VCALENDAR", ics);
        Assert.DoesNotContain("BEGIN:VEVENT", ics);
    }

    [Fact]
    public void One_off_entry_becomes_a_single_vevent_with_a_stable_uid()
    {
        var entry = OneOff("Standup", new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc));

        string ics = _builder.Build([entry]);

        Assert.Single(Occurrences(ics, "BEGIN:VEVENT"));
        Assert.Contains($"UID:{entry.Id}", ics);
        Assert.Contains("SUMMARY:Standup", ics);
        Assert.DoesNotContain("RRULE", ics);
    }

    [Fact]
    public void Recurring_entry_preserves_the_rrule_and_is_not_expanded()
    {
        var entry = new IcalEntry(
            Guid.NewGuid(), "Weekly", null,
            new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc),
            IsAllDay: false,
            RecurrenceRule: "FREQ=WEEKLY;BYDAY=FR;COUNT=5",
            ExcludedDates: [new DateOnly(2026, 8, 14)]);

        string ics = _builder.Build([entry]);

        // Exactly one VEVENT — the series is a single event carrying the rule, never five materialised events.
        Assert.Single(Occurrences(ics, "BEGIN:VEVENT"));
        Assert.Contains("RRULE:FREQ=WEEKLY", ics);
        Assert.Contains("BYDAY=FR", ics);
        Assert.Contains("EXDATE", ics);
    }

    [Fact]
    public void All_day_entry_is_emitted_as_a_date_value()
    {
        var entry = new IcalEntry(
            Guid.NewGuid(), "Holiday", null,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            IsAllDay: true, RecurrenceRule: null, ExcludedDates: []);

        string ics = _builder.Build([entry]);

        // DATE (no time-of-day) markers: VALUE=DATE on DTSTART/DTEND.
        Assert.Contains("DTSTART;VALUE=DATE:20260801", ics);
        Assert.Contains("DTEND;VALUE=DATE:20260802", ics); // DTEND is exclusive: the day after
    }

    private static IEnumerable<int> Occurrences(string haystack, string needle)
    {
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            yield return index;
            index += needle.Length;
        }
    }
}
