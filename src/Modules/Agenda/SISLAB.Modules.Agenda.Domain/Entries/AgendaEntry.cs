using SISLAB.Modules.Agenda.Domain.Entries.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Domain.Entries;

/// <summary>
/// The unified calendar entry aggregate (card [E10.1] #1) — the Google-Calendar-style event that supersedes the
/// activity-specific agenda types for the improved calendar. An entry is either a one-off (no
/// <see cref="RecurrenceRule"/>) or the head of a recurring series described by an RFC 5545 rule; recurring
/// occurrences are expanded on the read-side, never materialised as rows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rich behaviour, not an anemic bag.</b> All mutation goes through intention-revealing methods
/// (<see cref="Reschedule"/>, <see cref="Reallocate"/>, <see cref="CancelOccurrence"/>, <see cref="TruncateAt"/>)
/// that keep the invariants — end strictly after start, a recurring entry owning its own exclusion set — and
/// raise the matching domain event. The parameterless constructor exists only for EF materialisation.
/// </para>
/// <para>
/// <b>Recurrence editing (card [E10.2] #2).</b> The three Google-Calendar edit scopes are expressed as domain
/// operations the command handler orchestrates: "all occurrences" edits this aggregate directly; "only this"
/// excludes one date here (<see cref="CancelOccurrence"/>) and creates a detached one-off; "this and following"
/// truncates this series (<see cref="TruncateAt"/>) and creates a fresh series. The aggregate never knows the
/// edit-scope enum — it only exposes the primitive operations those scopes compose.
/// </para>
/// <para>
/// <b>Cross-module by value.</b> <see cref="ExperimentId"/> references an Experiments aggregate by its
/// <see cref="Guid"/> alone — no navigation, no FK across the schema boundary (module isolation, section 2).
/// The experiment's display name is resolved on the read-side through <c>Experiments.Contracts</c>.
/// </para>
/// </remarks>
public sealed class AgendaEntry : AggregateRoot<Guid>, ITenantEntity
{
    private readonly List<DateOnly> _excludedDates = [];
    private readonly List<EntryReminder> _reminders = [];

    public Guid CompanyId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public DateTime StartDateUtc { get; private set; }
    public DateTime EndDateUtc { get; private set; }
    public bool IsAllDay { get; private set; }
    public AgendaActivityType ActivityType { get; private set; }
    public Guid? ExperimentId { get; private set; }

    /// <summary>
    /// The room this entry occupies (card [E10.11]), referenced by value — no navigation, no FK across the
    /// aggregate boundary (module isolation, §2). Only meaningful for a
    /// <see cref="AgendaActivityType.RoomBooking"/> entry; <see langword="null"/> for every other activity type,
    /// and normalised to <see langword="null"/> by the factory/edit methods when the entry is not a room booking.
    /// </summary>
    public Guid? RoomId { get; private set; }
    public RecurrenceRuleSpec? RecurrenceRule { get; private set; }
    public Guid ResponsibleId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// The RFC 5545 <c>EXDATE</c> set — dates whose occurrence has been individually cancelled. Empty for a
    /// one-off entry. Read-only to the outside; mutated only via <see cref="CancelOccurrence"/>.
    /// </summary>
    public IReadOnlyList<DateOnly> ExcludedDates => _excludedDates.AsReadOnly();

    /// <summary>The reminders configured for this entry (card [E10.8] #5). Empty when none are set.</summary>
    public IReadOnlyList<EntryReminder> Reminders => _reminders.AsReadOnly();

    /// <summary><see langword="true"/> when the entry is the head of a recurring series.</summary>
    public bool IsRecurring => RecurrenceRule is not null;

    private AgendaEntry() : base(Guid.Empty) { }

    private AgendaEntry(
        Guid id,
        Guid companyId,
        string title,
        string? description,
        DateTime startDateUtc,
        DateTime endDateUtc,
        bool isAllDay,
        AgendaActivityType activityType,
        Guid? experimentId,
        Guid? roomId,
        RecurrenceRuleSpec? recurrenceRule,
        Guid responsibleId,
        DateTime createdAtUtc) : base(id)
    {
        CompanyId = companyId;
        Title = title;
        Description = description;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsAllDay = isAllDay;
        ActivityType = activityType;
        ExperimentId = experimentId;
        RoomId = NormaliseRoomId(activityType, roomId);
        RecurrenceRule = recurrenceRule;
        ResponsibleId = responsibleId;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Factory for a valid entry (card [E10.1] #1). Enforces a non-blank title and an end strictly after start
    /// (for a timed entry) and raises <see cref="AgendaEntryCreated"/>.
    /// </summary>
    public static AgendaEntry Create(
        Guid companyId,
        string title,
        string? description,
        DateTime startDateUtc,
        DateTime endDateUtc,
        bool isAllDay,
        AgendaActivityType activityType,
        Guid? experimentId,
        Guid? roomId,
        RecurrenceRuleSpec? recurrenceRule,
        Guid responsibleId,
        DateTime createdAtUtc,
        IEnumerable<EntryReminder>? reminders = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        GuardInterval(startDateUtc, endDateUtc, isAllDay);

        var entry = new AgendaEntry(
            Guid.NewGuid(),
            companyId,
            title.Trim(),
            description?.Trim(),
            startDateUtc,
            endDateUtc,
            isAllDay,
            activityType,
            experimentId,
            roomId,
            recurrenceRule,
            responsibleId,
            createdAtUtc);

        if (reminders is not null)
            entry.SetReminders(reminders);

        entry.RaiseDomainEvent(new AgendaEntryCreated(
            companyId, entry.Id, activityType, startDateUtc, recurrenceRule is not null));

        return entry;
    }

    /// <summary>
    /// Applies an "all occurrences" edit: overwrites the descriptive and scheduling fields (including the
    /// recurrence rule and experiment link) in place and raises <see cref="AgendaEntryUpdated"/>. The exclusion
    /// set is preserved — previously cancelled occurrences stay cancelled.
    /// </summary>
    public void Reschedule(
        string title,
        string? description,
        DateTime startDateUtc,
        DateTime endDateUtc,
        bool isAllDay,
        AgendaActivityType activityType,
        Guid? experimentId,
        Guid? roomId,
        RecurrenceRuleSpec? recurrenceRule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        GuardInterval(startDateUtc, endDateUtc, isAllDay);

        Title = title.Trim();
        Description = description?.Trim();
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsAllDay = isAllDay;
        ActivityType = activityType;
        ExperimentId = experimentId;
        RoomId = NormaliseRoomId(activityType, roomId);
        RecurrenceRule = recurrenceRule;

        RaiseDomainEvent(new AgendaEntryUpdated(CompanyId, Id, startDateUtc));
    }

    /// <summary>
    /// Reassigns the responsible person. Kept separate from <see cref="Reschedule"/> so a plain ownership
    /// change does not have to restate the whole schedule.
    /// </summary>
    public void Reallocate(Guid responsibleId)
    {
        ResponsibleId = responsibleId;
        RaiseDomainEvent(new AgendaEntryUpdated(CompanyId, Id, StartDateUtc));
    }

    /// <summary>
    /// Replaces the entry's reminder configuration with <paramref name="reminders"/> (card [E10.8] #5). Passing
    /// an empty set clears all reminders. Duplicate lead times (same minutes-before + channel) are collapsed so
    /// the same reminder never fires twice for one occurrence.
    /// </summary>
    public void SetReminders(IEnumerable<EntryReminder> reminders)
    {
        _reminders.Clear();

        foreach (EntryReminder reminder in reminders)
        {
            bool alreadyConfigured = _reminders.Any(existing =>
                existing.MinutesBefore == reminder.MinutesBefore
                && existing.NotificationType == reminder.NotificationType);

            if (!alreadyConfigured)
                _reminders.Add(reminder);
        }
    }

    /// <summary>
    /// Cancels a single occurrence of a recurring entry by adding <paramref name="occurrenceDate"/> to the
    /// exclusion set (RFC 5545 <c>EXDATE</c>). Idempotent — excluding an already-excluded date is a no-op.
    /// Throws <see cref="InvalidOperationException"/> for a one-off entry, which has no occurrences to exclude.
    /// </summary>
    public void CancelOccurrence(DateOnly occurrenceDate)
    {
        if (!IsRecurring)
            throw new InvalidOperationException(
                "Cannot cancel an occurrence of a non-recurring agenda entry; delete the entry instead.");

        if (_excludedDates.Contains(occurrenceDate))
            return;

        _excludedDates.Add(occurrenceDate);
        RaiseDomainEvent(new AgendaEntryOccurrenceCancelled(CompanyId, Id, occurrenceDate));
    }

    /// <summary>
    /// Truncates a recurring series so it stops before <paramref name="splitStartUtc"/>, by rewriting the rule's
    /// <c>UNTIL</c> to the instant just before the split (card [E10.2] #2, "this and following"). Throws
    /// <see cref="InvalidOperationException"/> for a one-off entry.
    /// </summary>
    public void TruncateAt(DateTime splitStartUtc)
    {
        if (!IsRecurring)
            throw new InvalidOperationException("Cannot truncate a non-recurring agenda entry.");

        // UNTIL is inclusive in RFC 5545; set it one second before the split so the split day is not emitted
        // by the (now-truncated) original series — the new series owns that day onward.
        RecurrenceRule = RecurrenceRule!.WithUntil(splitStartUtc.AddSeconds(-1));
        RaiseDomainEvent(new AgendaEntryUpdated(CompanyId, Id, StartDateUtc));
    }

    // A room association only makes sense for a room booking; drop any stray room id supplied for another
    // activity type so the invariant "RoomId is set only for a RoomBooking" holds without throwing on the
    // caller — the field is simply irrelevant for other types.
    private static Guid? NormaliseRoomId(AgendaActivityType activityType, Guid? roomId)
        => activityType == AgendaActivityType.RoomBooking ? roomId : null;

    private static void GuardInterval(DateTime startDateUtc, DateTime endDateUtc, bool isAllDay)
    {
        // An all-day entry may legitimately share its start/end day boundary; a timed entry must have a
        // strictly positive duration.
        if (isAllDay ? endDateUtc < startDateUtc : endDateUtc <= startDateUtc)
            throw new ArgumentException(
                "End must be after start (an all-day entry may end on the same instant).", nameof(endDateUtc));
    }
}
