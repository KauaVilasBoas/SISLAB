namespace SISLAB.Modules.Inventory.Contracts.Dtos;

/// <summary>
/// Public, flattened on-hand balance of a stock item, returned by
/// <see cref="IInventoryApi.GetOnHandBalanceAsync"/>. Carries only the current amount and its unit — the
/// minimal projection a consuming module needs to reconcile a balance without loading the aggregate or
/// depending on the Inventory Domain (module isolation, section 2).
/// </summary>
/// <param name="StockItemId">Stock item the balance refers to.</param>
/// <param name="QuantityValue">Current on-hand amount.</param>
/// <param name="QuantityUnit">Symbol of the item's unit of measure (e.g. "mL", "g").</param>
public sealed record StockBalanceDto(
    Guid StockItemId,
    decimal QuantityValue,
    string QuantityUnit);
