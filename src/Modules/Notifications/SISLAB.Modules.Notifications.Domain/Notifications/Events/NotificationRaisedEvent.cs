using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Notifications.Domain.Notifications.Events;

/// <summary>
/// Raised when a new <see cref="Notification"/> is created for the active company. Internal to the
/// Notifications bounded context. Unlike the Inventory events it is <b>not</b> translated to an
/// integration event on the Outbox: the notification is already the terminal effect of an alert (Option A),
/// not something to propagate further. The event exists so the aggregate stays the single source of truth
/// about its own lifecycle and so future in-context reactions (e.g. push/e-mail fan-out) have a hook.
/// </summary>
public sealed record NotificationRaisedEvent(
    Guid NotificationId,
    NotificationType Type,
    NotificationSeverity Severity) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
