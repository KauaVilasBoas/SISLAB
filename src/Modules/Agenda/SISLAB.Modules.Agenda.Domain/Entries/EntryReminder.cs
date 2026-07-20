using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Agenda.Domain.Entries;

/// <summary>
/// A configured reminder for an <see cref="AgendaEntry"/> (card [E10.8] #5): fire a notification
/// <see cref="MinutesBefore"/> minutes before each occurrence starts, over the given
/// <see cref="NotificationType"/> channel. Owned by the entry aggregate — it has no lifecycle of its own and
/// is only ever reached through its parent.
/// </summary>
/// <remarks>
/// The reminder is pure configuration: it does not track "already sent". Idempotency for recurring entries is
/// the reminder job's concern, handled with an occurrence-bucketed dedupe key on the notification (the same
/// approach as the presentation/biotério reminders), so a reminder can legitimately re-fire for each new
/// occurrence without the aggregate carrying per-occurrence sent-state.
/// </remarks>
public sealed class EntryReminder : Entity<Guid>
{
    public int MinutesBefore { get; private set; }
    public ReminderNotificationType NotificationType { get; private set; }

    private EntryReminder() : base(Guid.Empty) { }

    private EntryReminder(Guid id, int minutesBefore, ReminderNotificationType notificationType) : base(id)
    {
        MinutesBefore = minutesBefore;
        NotificationType = notificationType;
    }

    /// <summary>
    /// Creates a reminder that fires <paramref name="minutesBefore"/> minutes ahead of an occurrence. Throws
    /// <see cref="ArgumentOutOfRangeException"/> for a non-positive lead time.
    /// </summary>
    public static EntryReminder Create(int minutesBefore, ReminderNotificationType notificationType)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minutesBefore);
        return new EntryReminder(Guid.NewGuid(), minutesBefore, notificationType);
    }
}
