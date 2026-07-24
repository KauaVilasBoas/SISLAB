namespace SISLAB.Modules.Agenda.Contracts;

/// <summary>
/// Public command DTO to create one calendar entry from another module (SISLAB-10). The Experiments module builds
/// these when it generates an experiment schedule from an induction protocol and hands them to
/// <see cref="IAgendaScheduler"/>; it never touches the Agenda aggregate or DbContext.
/// </summary>
/// <remarks>
/// <para>
/// A plain POCO on the module's public boundary: it uses only BCL primitives and the Contracts-owned
/// <see cref="ScheduledActivityKind"/> enum, so a producer references <c>SISLAB.Modules.Agenda.Contracts</c> alone —
/// never the internal Domain (module-isolation rule, section 2). Cross-module ids (the linked experiment, the
/// responsible person) are carried <b>by value</b> as <see cref="Guid"/>s.
/// </para>
/// <para>
/// <b>Véspera reminders.</b> <see cref="ReminderMinutesBefore"/> is the lead time, in minutes, of an optional
/// reminder attached to the created entry. When set, the Agenda module's existing reminder mechanism fires an
/// in-app notification to the responsible that many minutes before the occurrence (e.g. 1440 = the day before),
/// which is how SISLAB-10's "lembrete de véspera" is delivered — no separate notification call is made here.
/// </para>
/// </remarks>
/// <param name="Title">Short headline shown on the calendar (e.g. "Coleta — Dia 28").</param>
/// <param name="Description">Optional one-line detail.</param>
/// <param name="StartUtc">Start instant of the entry (UTC).</param>
/// <param name="EndUtc">End instant of the entry (UTC); must be after <paramref name="StartUtc"/> for a timed entry.</param>
/// <param name="IsAllDay">Whether the entry spans the whole day (start/end may share the day boundary).</param>
/// <param name="Kind">The activity kind the entry represents.</param>
/// <param name="ExperimentId">The linked experiment, by value; <see langword="null"/> when not linked.</param>
/// <param name="ResponsibleId">The member responsible for this occurrence, by value — the roster's pick for the day.</param>
/// <param name="ReminderMinutesBefore">Optional véspera-reminder lead time in minutes; <see langword="null"/> for no reminder.</param>
public sealed record ScheduleAgendaEntryRequest(
    string Title,
    string? Description,
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsAllDay,
    ScheduledActivityKind Kind,
    Guid? ExperimentId,
    Guid ResponsibleId,
    int? ReminderMinutesBefore);
