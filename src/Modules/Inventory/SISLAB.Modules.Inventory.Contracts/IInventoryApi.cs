using SISLAB.Modules.Inventory.Contracts.Dtos;

namespace SISLAB.Modules.Inventory.Contracts;

/// <summary>
/// Public boundary of the Inventory module (card [E5] #35): the <b>only</b> surface other modules may
/// depend on to read inventory state. Every member returns primitives-only <c>*Dto</c> contracts owned
/// here — never the internal <c>StockItem</c> aggregate, its value objects or any EF type — so consuming
/// modules stay decoupled from the Inventory Domain/Application/Infrastructure (module isolation,
/// section 2; enforced by the architecture tests).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> Every operation is implicitly scoped to the active company. The
/// <c>CompanyId</c> is resolved by the adapter from <c>ITenantContext</c>, never passed by the caller —
/// a consuming module cannot read another tenant's inventory through this surface (defense-in-depth,
/// section 7).
/// </para>
/// <para>
/// <b>Read-only.</b> This is a query surface. State changes go through the module's own commands; there
/// is no cross-module mutation entry point by design (CQRS, section 2).
/// </para>
/// </remarks>
public interface IInventoryApi
{
    /// <summary>
    /// Returns the flattened summary of a single stock item of the active company, or
    /// <see langword="null"/> when no item with <paramref name="stockItemId"/> exists for that company.
    /// </summary>
    /// <param name="stockItemId">Identifier of the stock item to load.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<StockItemSummaryDto?> GetStockItemAsync(Guid stockItemId, CancellationToken ct);

    /// <summary>
    /// Returns <see langword="true"/> when a stock item with <paramref name="stockItemId"/> exists for
    /// the active company; otherwise <see langword="false"/>. Cheaper than
    /// <see cref="GetStockItemAsync"/> when only existence matters.
    /// </summary>
    /// <param name="stockItemId">Identifier of the stock item to probe.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> StockItemExistsAsync(Guid stockItemId, CancellationToken ct);

    /// <summary>
    /// Returns the current on-hand balance (amount + unit) of a stock item of the active company, or
    /// <see langword="null"/> when no such item exists for that company.
    /// </summary>
    /// <param name="stockItemId">Identifier of the stock item whose balance is requested.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<StockBalanceDto?> GetOnHandBalanceAsync(Guid stockItemId, CancellationToken ct);

    /// <summary>
    /// Lists the active company's stock items whose validity is at risk — expiring within the next
    /// <paramref name="daysAhead"/> days or already expired. Backs the E6 expiry-alert jobs, which scan
    /// with 30/15/7-day windows.
    /// </summary>
    /// <param name="daysAhead">Look-ahead window in days; a non-positive value falls back to the module default.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ExpiringItemDto>> ListExpiringItemsAsync(int daysAhead, CancellationToken ct);

    /// <summary>
    /// Lists the active company's stock items whose on-hand quantity has fallen below their configured
    /// minimum. Backs the E6 reposition-alert job.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<BelowMinimumItemDto>> ListItemsBelowMinimumAsync(CancellationToken ct);
}
