namespace SISLAB.Modules.Inventory.Infrastructure.ReadModels;

/// <summary>
/// Discriminator persisted in <c>inventory.stock_movements.movement_type</c>, identifying which kind of
/// stock movement a projected row represents. Stored as its name (see the projection's SQL), matching the
/// string-enum convention used elsewhere in the module.
/// </summary>
internal enum StockMovementType
{
    /// <summary>An incoming entry (receipt) that increased the balance.</summary>
    Received,

    /// <summary>A consumption that decreased the balance.</summary>
    Consumed,

    /// <summary>A move of the whole balance from one storage location to another (no quantity change).</summary>
    Transferred,

    /// <summary>A disposal (e.g. of an expired or unusable batch) that decreased the balance.</summary>
    Disposed
}
