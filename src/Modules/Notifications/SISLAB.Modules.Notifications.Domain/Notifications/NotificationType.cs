namespace SISLAB.Modules.Notifications.Domain.Notifications;

/// <summary>
/// The kind of laboratory condition a <see cref="Notification"/> reports. Each value corresponds to one of
/// the alert families the E6 jobs raise (validity #41, low stock #42, calibration/compliance #66) so the
/// bell can group and the operator can triage by nature of the risk.
/// </summary>
public enum NotificationType
{
    /// <summary>An item is expiring soon or already expired (validity window, job #41).</summary>
    Expiry = 1,

    /// <summary>An item's on-hand balance fell below its reorder threshold (low-stock, job #42).</summary>
    LowStock = 2,

    /// <summary>A piece of equipment is due for (or overdue on) calibration (job #66).</summary>
    Calibration = 3,

    /// <summary>A controlled item requires a compliance action (controlled-substances bookkeeping, job #66).</summary>
    ControlledCompliance = 4
}
