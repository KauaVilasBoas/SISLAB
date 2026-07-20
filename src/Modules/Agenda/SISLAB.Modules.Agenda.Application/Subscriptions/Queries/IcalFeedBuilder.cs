using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;

namespace SISLAB.Modules.Agenda.Application.Subscriptions.Queries;

/// <summary>
/// Serializes agenda entries into an RFC 5545 iCalendar document (card [E10.10]) with Ical.Net, so the module
/// never hand-rolls the .ics wire format. Each <see cref="IcalEntry"/> becomes one <c>VEVENT</c>; a recurring
/// entry keeps its stored <c>RRULE</c> and <c>EXDATE</c> verbatim so the subscribing calendar client expands the
/// series itself — we deliberately do <b>not</b> expand occurrences here (unlike the calendar read-side).
/// </summary>
/// <remarks>
/// Stateless and thread-safe — register as a singleton. All times are emitted in UTC. The stable per-entry
/// <c>UID</c> (the entry id) lets clients update an event in place across polls instead of duplicating it.
/// Ical.Net 4.2.0 always stamps its own <c>PRODID</c> on serialize, so we do not override it.
/// </remarks>
public sealed class IcalFeedBuilder
{
    public string Build(IReadOnlyList<IcalEntry> entries)
    {
        var calendar = new Calendar();

        foreach (IcalEntry entry in entries)
            calendar.Events.Add(ToEvent(entry));

        return new CalendarSerializer().SerializeToString(calendar);
    }

    private static CalendarEvent ToEvent(IcalEntry entry)
    {
        var calendarEvent = new CalendarEvent
        {
            Uid = entry.Id.ToString(),
            Summary = entry.Title,
            Description = entry.Description,
        };

        ApplyTiming(calendarEvent, entry);
        ApplyRecurrence(calendarEvent, entry);

        return calendarEvent;
    }

    private static void ApplyTiming(CalendarEvent calendarEvent, IcalEntry entry)
    {
        if (entry.IsAllDay)
        {
            // All-day events are DATE values (no time-of-day); DTEND is exclusive, so it is the day after. The
            // (y, m, d) constructor yields a date-only CalDateTime (HasTime == false) so it serialises as VALUE=DATE.
            DateOnly startDay = DateOnly.FromDateTime(entry.StartUtc);
            DateOnly endDayExclusive = DateOnly.FromDateTime(entry.EndUtc).AddDays(1);
            calendarEvent.Start = new CalDateTime(startDay.Year, startDay.Month, startDay.Day);
            calendarEvent.End = new CalDateTime(endDayExclusive.Year, endDayExclusive.Month, endDayExclusive.Day);
            calendarEvent.IsAllDay = true;
            return;
        }

        calendarEvent.Start = new CalDateTime(DateTime.SpecifyKind(entry.StartUtc, DateTimeKind.Utc));
        calendarEvent.End = new CalDateTime(DateTime.SpecifyKind(entry.EndUtc, DateTimeKind.Utc));
    }

    private static void ApplyRecurrence(CalendarEvent calendarEvent, IcalEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RecurrenceRule))
            return;

        calendarEvent.RecurrenceRules.Add(new RecurrencePattern(entry.RecurrenceRule));

        foreach (DateOnly excluded in entry.ExcludedDates)
        {
            // The EXDATE must match the recurrence instant's time-of-day, so key it off the series start time.
            var exdate = new CalDateTime(
                excluded.ToDateTime(TimeOnly.FromDateTime(entry.StartUtc), DateTimeKind.Utc));
            calendarEvent.ExceptionDates.Add(new PeriodList { exdate });
        }
    }
}
