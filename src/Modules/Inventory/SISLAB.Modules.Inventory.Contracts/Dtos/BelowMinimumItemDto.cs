namespace SISLAB.Modules.Inventory.Contracts.Dtos;

/// <summary>
/// Public, flattened row describing a stock item whose on-hand quantity has fallen below its configured
/// minimum, returned by <see cref="IInventoryApi.ListItemsBelowMinimumAsync"/>. It is the cross-module
/// projection the E6 reposition-alert job consumes — carrying only primitives, never the internal
/// <c>StockItem</c> aggregate (module isolation, section 2).
/// </summary>
/// <remarks>
/// The current and minimum amounts are surfaced with their own units so consumers can render or compare
/// them without assuming the pairing; the two share the item's canonical unit. The deficit
/// (minimum − current) is intentionally left for the consumer to derive from these primitives.
/// </remarks>
/// <param name="StockItemId">Stock item identifier.</param>
/// <param name="Name">Human-readable item name.</param>
/// <param name="CurrentQuantityValue">Current on-hand amount (below the minimum).</param>
/// <param name="CurrentQuantityUnit">Symbol of the current quantity's unit of measure.</param>
/// <param name="MinimumQuantityValue">Configured minimum stock amount.</param>
/// <param name="MinimumQuantityUnit">Symbol of the minimum's unit of measure.</param>
public sealed record BelowMinimumItemDto(
    Guid StockItemId,
    string Name,
    decimal CurrentQuantityValue,
    string CurrentQuantityUnit,
    decimal MinimumQuantityValue,
    string MinimumQuantityUnit);
