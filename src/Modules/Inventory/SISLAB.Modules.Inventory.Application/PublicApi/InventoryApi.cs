using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.Modules.Inventory.Contracts;
using SISLAB.Modules.Inventory.Contracts.Dtos;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.PublicApi;

/// <summary>
/// Adapter implementing the Inventory module's public boundary <see cref="IInventoryApi"/> (card [E5]
/// #35). It is the single place that translates the module's internal read models (the E4/E5 Dapper
/// queries) into the primitives-only <c>*Dto</c> contracts other modules consume — nothing of the
/// Inventory Domain/EF crosses the boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Delegation, not re-implementation.</b> Every operation dispatches an existing query through
/// <see cref="IMediator"/> and maps its result — there is no SQL here. The by-id operations share
/// <see cref="GetStockItemDetailQuery"/> (one round-trip, projected three ways); the alert listings reuse
/// the E4 <see cref="ListExpiringItemsQuery"/> (#30) and <see cref="ListItemsBelowMinimumQuery"/> (#32).
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The adapter passes no company id: it lives inside the module, so the queries it
/// dispatches resolve the active company from <c>ITenantContext</c> themselves and keep the mandatory
/// <c>WHERE company_id = @CompanyId</c>. A caller cannot target another tenant through this surface.
/// </para>
/// <para>
/// <b>Unpaged listings.</b> The alert operations return the whole at-risk/below-minimum set (the E6 jobs
/// want every item, not a page), so the adapter walks the paginated E4 queries to exhaustion via the
/// shared <see cref="PagedQueryDrainer"/> — bounded by the first page's <c>TotalPages</c>, so it never
/// issues an unbounded number of round-trips.
/// </para>
/// </remarks>
internal sealed class InventoryApi : IInventoryApi
{
    // The E4 PagedQuery clamps PageSize to 200; request the maximum so the exhaustion walk uses the
    // fewest round-trips for the low-volume LAFTE alert scans.
    private const int MaxPageSize = 200;

    private readonly IMediator _mediator;

    public InventoryApi(IMediator mediator) => _mediator = mediator;

    /// <inheritdoc />
    public async Task<StockItemSummaryDto?> GetStockItemAsync(Guid stockItemId, CancellationToken ct)
    {
        StockItemDetail? detail = await _mediator.SendAsync(new GetStockItemDetailQuery(stockItemId), ct);

        return detail is null ? null : ToSummaryDto(detail);
    }

    /// <inheritdoc />
    public async Task<bool> StockItemExistsAsync(Guid stockItemId, CancellationToken ct)
    {
        StockItemDetail? detail = await _mediator.SendAsync(new GetStockItemDetailQuery(stockItemId), ct);

        return detail is not null;
    }

    /// <inheritdoc />
    public async Task<StockBalanceDto?> GetOnHandBalanceAsync(Guid stockItemId, CancellationToken ct)
    {
        StockItemDetail? detail = await _mediator.SendAsync(new GetStockItemDetailQuery(stockItemId), ct);

        return detail is null ? null : ToBalanceDto(detail);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExpiringItemDto>> ListExpiringItemsAsync(
        int daysAhead,
        CancellationToken ct)
    {
        List<ExpiringItemDto> results = new();

        await foreach (ExpiringItem item in PagedQueryDrainer.StreamAsync(
            _mediator,
            page => new ListExpiringItemsQuery
            {
                WarningWindowDays = daysAhead,
                IncludeExpired = true,
                Page = page,
                PageSize = MaxPageSize
            },
            ct))
        {
            results.Add(ToExpiringDto(item));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BelowMinimumItemDto>> ListItemsBelowMinimumAsync(CancellationToken ct)
    {
        List<BelowMinimumItemDto> results = new();

        await foreach (BelowMinimumItem item in PagedQueryDrainer.StreamAsync(
            _mediator,
            page => new ListItemsBelowMinimumQuery
            {
                Page = page,
                PageSize = MaxPageSize
            },
            ct))
        {
            results.Add(ToBelowMinimumDto(item));
        }

        return results;
    }

    private static StockItemSummaryDto ToSummaryDto(StockItemDetail detail) => new(
        Id: detail.Id,
        Name: detail.Name,
        Category: detail.Category,
        QuantityValue: detail.Quantity,
        QuantityUnit: detail.Unit,
        MinimumQuantityValue: detail.MinimumQuantity,
        MinimumQuantityUnit: detail.MinimumUnit,
        ExpiryYear: detail.ExpiryYear,
        ExpiryMonth: detail.ExpiryMonth,
        StorageLocationId: detail.StorageLocationId,
        StorageLocationName: detail.StorageLocationName,
        IsControlled: detail.IsControlled,
        CompanyId: detail.CompanyId);

    private static StockBalanceDto ToBalanceDto(StockItemDetail detail) => new(
        StockItemId: detail.Id,
        QuantityValue: detail.Quantity,
        QuantityUnit: detail.Unit);

    private static ExpiringItemDto ToExpiringDto(ExpiringItem item) => new(
        StockItemId: item.Id,
        Name: item.Name,
        ExpiryYear: item.ExpiryYear,
        ExpiryMonth: item.ExpiryMonth,
        StorageLocationId: item.StorageLocationId,
        StorageLocationName: item.StorageLocationName);

    private static BelowMinimumItemDto ToBelowMinimumDto(BelowMinimumItem item) => new(
        StockItemId: item.Id,
        Name: item.Name,
        CurrentQuantityValue: item.Quantity,
        CurrentQuantityUnit: item.Unit,
        MinimumQuantityValue: item.MinimumQuantity,
        MinimumQuantityUnit: item.MinimumUnit);
}
