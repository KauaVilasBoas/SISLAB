namespace SISLAB.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Idempotent write port for raising a notification. It performs an <c>INSERT ... ON CONFLICT DO NOTHING</c>
/// against the partial unique index over active (unread) rows, so raising the same alert twice (same company +
/// dedupe key, still unread) inserts at most one row — the same principle as the Inventory
/// <c>StockMovementStore</c>.
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
}
