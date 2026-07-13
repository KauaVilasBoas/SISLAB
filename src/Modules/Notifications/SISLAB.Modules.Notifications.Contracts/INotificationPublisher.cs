namespace SISLAB.Modules.Notifications.Contracts;

/// <summary>
/// Public port to raise a notification into the Notifications module (card #64a, Option A). It is the single
/// inbound seam other parts of the system use to surface an alert in the bell: the E6 jobs (#41/#42/#66)
/// resolve this from the shared DI container (API + Jobs run in one process) and call
/// <see cref="RaiseAsync"/>; they never reference the module's Domain, Application or Infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// The implementation (in the module's Infrastructure) translates the <see cref="RaiseNotificationRequest"/>
/// into the <c>Notification</c> aggregate and persists it directly on the module's write side, synchronously,
/// <b>without</b> going through the Outbox — the notification is already the terminal effect of the alert, not
/// an event to propagate onward.
/// </para>
/// <para>
/// The call is <b>idempotent</b> by <see cref="RaiseNotificationRequest.DedupeKey"/>: raising the same alert
/// twice (same key, same company) yields at most one active notification. Callers may therefore re-raise
/// freely each cycle without deduplicating themselves.
/// </para>
/// </remarks>
public interface INotificationPublisher
{
    /// <summary>
    /// Raises a notification for the active company, idempotently by dedupe key. Returns <see langword="true"/>
    /// when a new notification was created, or <see langword="false"/> when an active notification with the
    /// same dedupe key already existed and the request was a no-op.
    /// </summary>
    Task<bool> RaiseAsync(RaiseNotificationRequest request, CancellationToken cancellationToken = default);
}
