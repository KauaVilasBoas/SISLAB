namespace SISLAB.Modules.Notifications.Contracts;

/// <summary>
/// Public code for the family of condition a notification reports, mirrored on the module's boundary so the
/// <see cref="RaiseNotificationRequest"/> DTO stays independent of the internal domain enum. The publisher
/// maps each code to the corresponding domain <c>NotificationType</c>. Numeric values are pinned to match the
/// domain enum one-to-one, so the mapping is a stable, total function.
/// </summary>
public enum NotificationTypeCode
{
    /// <summary>An item is expiring soon or already expired (validity window, job #41).</summary>
    Expiry = 1,

    /// <summary>An item's balance fell below its reorder threshold (low-stock, job #42).</summary>
    LowStock = 2,

    /// <summary>Equipment is due for (or overdue on) calibration (job #66).</summary>
    Calibration = 3,

    /// <summary>A controlled item requires a compliance action (job #66).</summary>
    ControlledCompliance = 4,

    /// <summary>A presentation is approaching its 15-day material deadline (job [E6] #83).</summary>
    PresentationReminder = 5,

    /// <summary>A biotério cage-cleaning assignment is due this week (job [E6] #83).</summary>
    BioteriumReminder = 6,

    /// <summary>A calendar entry occurrence is approaching its configured reminder lead time (job [E10.8] #5).</summary>
    AgendaReminder = 7,
}
