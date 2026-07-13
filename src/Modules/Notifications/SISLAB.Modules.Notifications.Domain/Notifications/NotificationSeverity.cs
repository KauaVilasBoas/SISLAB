namespace SISLAB.Modules.Notifications.Domain.Notifications;

/// <summary>
/// How urgently the operator should act on a <see cref="Notification"/>. Drives the colour of the bell badge
/// and the ordering of the list (critical first). Kept as a small closed enum rather than a value object: it
/// carries no behaviour or invariant beyond the set of allowed levels, so an enum is the honest model.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>Purely informational — no action strictly required (e.g. an item expiring far out).</summary>
    Info = 1,

    /// <summary>Action is advisable soon (e.g. stock nearing the minimum, calibration approaching).</summary>
    Warning = 2,

    /// <summary>Action is required now (e.g. an expired controlled item, stock exhausted).</summary>
    Critical = 3
}
