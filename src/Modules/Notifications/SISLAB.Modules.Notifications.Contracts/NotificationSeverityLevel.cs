namespace SISLAB.Modules.Notifications.Contracts;

/// <summary>
/// Public level for how urgently a notification should be acted on, mirrored on the module's boundary so the
/// <see cref="RaiseNotificationRequest"/> DTO does not leak the internal domain enum. The publisher maps each
/// level to the corresponding domain <c>NotificationSeverity</c>. Numeric values match the domain enum
/// one-to-one.
/// </summary>
public enum NotificationSeverityLevel
{
    /// <summary>Purely informational — no action strictly required.</summary>
    Info = 1,

    /// <summary>Action is advisable soon.</summary>
    Warning = 2,

    /// <summary>Action is required now.</summary>
    Critical = 3
}
