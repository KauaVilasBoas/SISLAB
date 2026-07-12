namespace SISLAB.Modules.Inventory.Contracts.Dtos;

/// <summary>
/// Public, flattened summary of a single stock item, returned across the module boundary by
/// <see cref="IInventoryApi.GetStockItemAsync"/>. It carries only primitives — never the internal
/// <c>StockItem</c> aggregate or its value objects (Quantity, UnitOfMeasure, ExpiryDate) — so a
/// consuming module depends on nothing of the Inventory Domain (module isolation, section 2).
/// </summary>
/// <remarks>
/// Validity is exposed with the same month granularity the aggregate stores it in: an item is valid
/// through the last day of (<paramref name="ExpiryYear"/>, <paramref name="ExpiryMonth"/>). Both are
/// <see langword="null"/> together when the item has no recorded validity. The minimum quantity shares
/// the item's canonical unit, but its amount and unit are surfaced explicitly so consumers never assume
/// the pairing.
/// </remarks>
/// <param name="Id">Stock item identifier.</param>
/// <param name="Name">Human-readable item name.</param>
/// <param name="Category">Category name (the <c>StockItemCategory</c> enum name).</param>
/// <param name="QuantityValue">Current on-hand amount.</param>
/// <param name="QuantityUnit">Symbol of the item's unit of measure (e.g. "mL", "g").</param>
/// <param name="MinimumQuantityValue">Configured minimum stock amount.</param>
/// <param name="MinimumQuantityUnit">Symbol of the minimum's unit of measure.</param>
/// <param name="ExpiryYear">Expiry year, or <see langword="null"/> when the item has no validity.</param>
/// <param name="ExpiryMonth">Expiry month (1-12), or <see langword="null"/> when the item has no validity.</param>
/// <param name="StorageLocationId">Identifier of the item's storage location.</param>
/// <param name="StorageLocationName">Name of the storage location, or <see langword="null"/> when unresolved.</param>
/// <param name="IsControlled">True when the item is a controlled substance requiring extra traceability.</param>
/// <param name="CompanyId">Owning company (tenant) the item belongs to.</param>
public sealed record StockItemSummaryDto(
    Guid Id,
    string Name,
    string Category,
    decimal QuantityValue,
    string QuantityUnit,
    decimal MinimumQuantityValue,
    string MinimumQuantityUnit,
    int? ExpiryYear,
    int? ExpiryMonth,
    Guid StorageLocationId,
    string? StorageLocationName,
    bool IsControlled,
    Guid CompanyId);
