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

    /// <summary>
    /// Acknowledges <b>all</b> of the active company's unread notifications in one set-based operation, stamping
    /// the read instant with <paramref name="readAtUtc"/> (from <c>IClock</c>, never the database clock), and
    /// returns how many were flipped. Idempotent: with nothing unread it changes nothing and returns <c>0</c>.
    /// </summary>
    /// <remarks>
    /// This is a deliberate bulk exception to the "mutate one loaded aggregate" rule: acknowledging the whole
    /// inbox is a single UPDATE over the unread rows, so it does not load N aggregates just to flip a flag. It is
    /// implicitly tenant-scoped to the active company; the read event of each row is not raised, which is
    /// intentional — under Option A a notification is a terminal effect with no downstream consumer of the read
    /// event (the module wires a no-op domain-event dispatcher).
    /// </remarks>
    Task<int> MarkAllAsReadForActiveCompanyAsync(DateTime readAtUtc, CancellationToken ct = default);
}
