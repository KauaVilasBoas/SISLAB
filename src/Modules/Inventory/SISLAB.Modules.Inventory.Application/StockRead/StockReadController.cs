using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StockRead;

/// <summary>
/// Read-side (CQRS query) HTTP boundary for the inventory master-detail screen (#46) of the
/// <b>active company</b>: the paginated item listing (filterable by location/category, with free-text
/// search) and the per-location summary. The controller only dispatches queries through
/// <see cref="IMediator"/> and wraps the result in the uniform <see cref="ApiResult{T}"/> envelope; it
/// never touches Dapper, the DbContext or repositories, and never maps errors — those bubble up to the
/// exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: the active company is resolved from the httpOnly cookie into <c>ITenantContext</c> and
/// read by the query handlers (which keep the mandatory <c>WHERE company_id = @CompanyId</c>), never from
/// the request. Kept separate from the write-side <c>StockItemsController</c> to honour CQRS: reads and
/// writes have independent handlers and contracts.
/// </remarks>
[Route("api/inventory")]
[Authorize]
public sealed class StockReadController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public StockReadController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lists the active company's stock items for the inventory table, optionally filtered by storage
    /// location, category and free-text search, ordered by name and paginated.
    /// </summary>
    [HttpGet("stock-items")]
    [ProducesResponseType(typeof(ApiResult<PagedResult<StockItemListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListStockItems(
        [FromQuery] Guid? storageLocationId,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<StockItemListItem> result = await _mediator.SendAsync(
            new ListStockItemsQuery
            {
                StorageLocationId = storageLocationId,
                Category = category,
                Search = search,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<StockItemListItem>>(true, "Stock items retrieved.", result));
    }

    /// <summary>
    /// Returns the per-location summary (item count, expired count, critical flag) for the master-detail
    /// left column, including empty locations.
    /// </summary>
    [HttpGet("storage-locations/summary")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<LocationSummaryItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLocationsSummary(CancellationToken ct)
    {
        IReadOnlyList<LocationSummaryItem> summary = await _mediator.SendAsync(
            new GetLocationsSummaryQuery(),
            ct);

        return Ok(new ApiResult<IReadOnlyList<LocationSummaryItem>>(true, "Locations summary retrieved.", summary));
    }
}
