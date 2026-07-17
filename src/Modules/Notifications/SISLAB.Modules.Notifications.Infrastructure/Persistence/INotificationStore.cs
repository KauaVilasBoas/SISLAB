namespace SISLAB.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Idempotent, set-based write port for the notification table. Raising a notification performs an
/// <c>INSERT ... ON CONFLICT DO NOTHING</c> against the partial unique index over active (unread) rows, so
/// raising the same alert twice (same company + dedupe key, still unread) inserts at most one row — the same
/// principle as the Inventory <c>StockMovementStore</c>. Marking all as read performs one bulk UPDATE over the
/// company's unread rows.
/// </summary>
/// <remarks>
/// This lives on the write side but uses Dapper rather than <c>SaveChanges</c> on purpose: EF's tracked insert
/// would throw <c>DbUpdateException</c> on the unique-index conflict, whereas <c>ON CONFLICT DO NOTHING</c>
/// resolves the race to a clean no-op. The store is the single seam the <c>INotificationPublisher</c> writes
/// through.
/// </remarks>
internal interface INotificationStore
{
    /// <summary>
    /// Inserts the notification row idempotently. Returns <see langword="true"/> when a new row was written,
    /// or <see langword="false"/> when an active notification with the same (company, dedupe key) already
    /// existed and the insert was skipped.
    /// </summary>
    Task<bool> TryAppendAsync(NotificationRow row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks <b>every</b> unread notification of the given company as read in a single set-based UPDATE, stamping
    /// <c>read_at_utc</c> with <paramref name="readAtUtc"/>. Idempotent: when the company has no unread rows the
    /// statement affects nothing and returns <c>0</c> (a clean no-op). Returns the number of rows flipped, so the
    /// caller can report how many were acknowledged. Scoped to the active company via the explicit
    /// <c>WHERE company_id</c> predicate — the read/write Dapper path has no EF global query filter.
    /// </summary>
    Task<int> MarkAllReadAsync(Guid companyId, DateTime readAtUtc, CancellationToken cancellationToken = default);
}
