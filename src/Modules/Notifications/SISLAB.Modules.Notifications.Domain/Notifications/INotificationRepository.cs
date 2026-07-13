namespace SISLAB.Modules.Notifications.Domain.Notifications;

/// <summary>
/// Repository for the <see cref="Notification"/> aggregate. The concrete implementation lives in the module's
/// Infrastructure project (EF Core). Lookups are implicitly tenant-scoped by the write-side global query
/// filter. Raising a notification is idempotent by dedupe key and is handled by the write path (partial
/// unique index + <c>ON CONFLICT DO NOTHING</c>), so this interface deliberately exposes only the operations
/// the command side needs — a plain <c>Add</c> is not offered because it cannot express the idempotency.
/// </summary>
public interface INotificationRepository
{
    /// <summary>Loads a notification by id within the active company, or <see langword="null"/> if absent.</summary>
    Task<Notification?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Stages a mutation to an already-tracked notification (e.g. after <c>MarkAsRead</c>).</summary>
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
}
