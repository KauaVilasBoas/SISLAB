namespace SISLAB.Modules.Inventory.Infrastructure.ReadModels;

/// <summary>
/// Persistence gateway for the <c>inventory.stock_movements</c> read model. Isolates the Dapper/SQL write
/// of a projected movement from <see cref="StockMovementProjectionHandler"/>, so the handler holds only
/// the event→row translation and the store owns the idempotent insert. This seam also keeps the handler
/// unit-testable without a live database.
/// </summary>
internal interface IStockMovementStore
{
    /// <summary>
    /// Inserts a projected movement, keyed by <see cref="StockMovementRow.Id"/> (the event id). The
    /// operation MUST be idempotent: appending a row whose id already exists is a no-op, never a
    /// duplicate and never an error — so redelivery of the same integration event is safe.
    /// </summary>
    Task AppendAsync(StockMovementRow row, CancellationToken cancellationToken = default);
}
