using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Notifications.Domain.Notifications.Events;

/// <summary>
/// Raised the first time a <see cref="Notification"/> is marked as read. Idempotent by construction: a
/// notification that is already read does not re-raise it, so consumers (e.g. an unread-count cache) see the
/// transition exactly once.
/// </summary>
public sealed record NotificationReadEvent(Guid NotificationId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
