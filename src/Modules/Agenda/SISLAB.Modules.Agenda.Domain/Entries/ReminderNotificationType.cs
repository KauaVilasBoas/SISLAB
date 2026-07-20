namespace SISLAB.Modules.Agenda.Domain.Entries;

/// <summary>
/// The channel a configured reminder is delivered through (card [E10.8] #5). Kept as a domain enum so the
/// Agenda domain does not depend on the Notifications module; the reminder job maps it to the public
/// notification type when raising the alert. In-app is the only channel in the MVP; the enum leaves room for
/// email/push later without a schema change (persisted by name).
/// </summary>
public enum ReminderNotificationType
{
    /// <summary>An in-app notification surfaced in the bell.</summary>
    InApp = 1,
}
