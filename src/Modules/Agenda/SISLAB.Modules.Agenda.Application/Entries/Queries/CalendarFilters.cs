using SISLAB.Modules.Agenda.Domain.Entries;

namespace SISLAB.Modules.Agenda.Application.Entries.Queries;

/// <summary>
/// Optional server-side filters for the calendar read (cards [E10.4] #4 / [E10.7]). All are nullable/opt-in:
/// a null field does not constrain the result. <see cref="OnlyMine"/> combined with
/// <see cref="CurrentUserId"/> restricts the calendar to the caller's own entries ("My agenda").
/// </summary>
/// <param name="ActivityType">When set, only entries of this activity type.</param>
/// <param name="ResponsibleId">When set, only entries owned by this person.</param>
/// <param name="ExperimentId">When set, only entries linked to this experiment.</param>
/// <param name="OnlyMine">When true, restrict to entries owned by <paramref name="CurrentUserId"/>.</param>
/// <param name="CurrentUserId">The authenticated user, supplied by the controller (never the client body).</param>
public sealed record CalendarFilters(
    AgendaActivityType? ActivityType = null,
    Guid? ResponsibleId = null,
    Guid? ExperimentId = null,
    bool OnlyMine = false,
    Guid? CurrentUserId = null);
