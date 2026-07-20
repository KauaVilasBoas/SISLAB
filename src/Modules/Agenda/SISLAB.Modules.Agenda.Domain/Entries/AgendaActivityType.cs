namespace SISLAB.Modules.Agenda.Domain.Entries;

/// <summary>
/// The kind of activity an <see cref="AgendaEntry"/> represents (card [E10.1] #1). Drives the colour the
/// front-end paints the event with and lets the calendar filter by activity. Persisted as its string name
/// (never the ordinal) so re-ordering the enum can never silently re-map stored rows.
/// </summary>
public enum AgendaActivityType
{
    /// <summary>A room reservation — the only type the room-conflict / occupancy views consider.</summary>
    RoomBooking = 1,

    /// <summary>An experiment run, optionally linked to an <see cref="AgendaEntry.ExperimentId"/>.</summary>
    Experiment = 2,

    /// <summary>A biotério (animal facility) chore such as cage cleaning.</summary>
    Bioterium = 3,

    /// <summary>A seminar / journal-club presentation.</summary>
    Presentation = 4,

    /// <summary>Anything that does not fit the specific types above.</summary>
    Other = 5,
}
