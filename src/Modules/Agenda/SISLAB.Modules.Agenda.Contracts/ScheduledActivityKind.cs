namespace SISLAB.Modules.Agenda.Contracts;

/// <summary>
/// Public code for the kind of activity a scheduled agenda entry represents (SISLAB-10), mirrored on the module's
/// boundary so the <see cref="ScheduleAgendaEntryRequest"/> DTO stays independent of the internal
/// <c>AgendaActivityType</c> enum. The adapter maps each code to the corresponding domain activity type. Numeric
/// values are pinned to match that domain enum one-to-one, so the mapping is a stable, total function.
/// </summary>
public enum ScheduledActivityKind
{
    /// <summary>A room reservation.</summary>
    RoomBooking = 1,

    /// <summary>An experiment run, linked to an experiment by value.</summary>
    Experiment = 2,

    /// <summary>A biotério (animal facility) chore such as cage cleaning.</summary>
    Bioterium = 3,

    /// <summary>A seminar / journal-club presentation.</summary>
    Presentation = 4,

    /// <summary>Anything that does not fit the specific kinds above.</summary>
    Other = 5,
}
