namespace SISLAB.Modules.Inventory.Contracts.Dtos;

/// <summary>
/// Public, flattened row describing a stock item whose validity is at risk, returned by
/// <see cref="IInventoryApi.ListExpiringItemsAsync"/>. It is the cross-module projection the E6 alert
/// jobs consume to raise expiry notifications — carrying only primitives, never the internal
/// <c>StockItem</c> aggregate (module isolation, section 2).
/// </summary>
/// <remarks>
/// Only items that carry a validity can be at risk, so <paramref name="ExpiryYear"/> and
/// <paramref name="ExpiryMonth"/> are always populated here (unlike <see cref="StockItemSummaryDto"/>,
/// where they are nullable). The item is valid through the last day of that month.
/// </remarks>
/// <param name="StockItemId">Stock item identifier.</param>
/// <param name="Name">Human-readable item name.</param>
/// <param name="ExpiryYear">Expiry year.</param>
/// <param name="ExpiryMonth">Expiry month (1-12).</param>
/// <param name="StorageLocationId">Identifier of the item's storage location.</param>
/// <param name="StorageLocationName">Name of the storage location, or <see langword="null"/> when unresolved.</param>
public sealed record ExpiringItemDto(
    Guid StockItemId,
    string Name,
    int ExpiryYear,
    int ExpiryMonth,
    Guid StorageLocationId,
    string? StorageLocationName);
