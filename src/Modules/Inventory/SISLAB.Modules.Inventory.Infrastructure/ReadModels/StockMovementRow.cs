namespace SISLAB.Modules.Inventory.Infrastructure.ReadModels;

/// <summary>
/// Flat, primitive row of the <c>inventory.stock_movements</c> read model — one row per stock movement.
/// Built by <see cref="StockMovementProjectionHandler"/> from an integration event and persisted by an
/// <see cref="IStockMovementStore"/>.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is the originating integration event's <c>EventId</c>: it is the row's identity and
/// the deduplication key, so reprocessing the same event is a no-op (idempotency). <see cref="PerformedBy"/>
/// (responsável) is not populated by the projection — the Inventory module has no user identity (decision
/// on card [E3] #24); the audit trail (card #57 / E9) owns it and may backfill later.
/// </remarks>
internal sealed record StockMovementRow(
    Guid Id,
    Guid CompanyId,
    Guid StockItemId,
    string MovementType,
    decimal QuantityAmount,
    string QuantityUnit,
    DateOnly OccurredOn,
    Guid? ExperimentId,
    Guid? PartnerId,
    Guid? StockBatchId,
    decimal? UnitCostBrl,
    DateTime CreatedAtUtc)
{
    /// <summary>Always null from this projection — see the type remarks.</summary>
    public Guid? PerformedBy => null;
}
