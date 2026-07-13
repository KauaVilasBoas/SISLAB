using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Queries;

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

    /// <summary>
    /// Lists the active company's stock items whose validity is at risk — expiring within the given window
    /// (default 30 days) and, unless <paramref name="includeExpired"/> is false, already expired — ordered
    /// by validity ascending and paginated, each carrying its signed days-remaining. Feeds the expiry screen
    /// and the validity job (which passes 30/15/7-day windows).
    /// </summary>
    [HttpGet("stock-items/expiring")]
    [ProducesResponseType(typeof(ApiResult<PagedResult<ExpiringItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListExpiringItems(
        [FromQuery] int warningWindowDays = ExpiryStatusRule.DefaultWarningWindowDays,
        [FromQuery] bool includeExpired = true,
        [FromQuery] Guid? storageLocationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<ExpiringItem> result = await _mediator.SendAsync(
            new ListExpiringItemsQuery
            {
                WarningWindowDays = warningWindowDays,
                IncludeExpired = includeExpired,
                StorageLocationId = storageLocationId,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<ExpiringItem>>(true, "Expiring items retrieved.", result));
    }

    /// <summary>
    /// Returns the three "Situação de validade" donut totals for the active company — expired, expiring
    /// within 30 days and comfortably valid — over items that carry a validity (dashboard #49).
    /// </summary>
    [HttpGet("stock-items/expiry-summary")]
    [ProducesResponseType(typeof(ApiResult<ExpirySummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetExpirySummary(CancellationToken ct)
    {
        ExpirySummary summary = await _mediator.SendAsync(new GetExpirySummaryQuery(), ct);

        return Ok(new ApiResult<ExpirySummary>(true, "Expiry summary retrieved.", summary));
    }

    /// <summary>
    /// Lists the active company's stock items whose on-hand quantity is below the configured minimum,
    /// optionally filtered by storage location, ordered by criticality (largest deficit first) and
    /// paginated. Each row carries its current quantity, minimum and derived deficit. Backs the low-stock
    /// list and the reposition-alert job.
    /// </summary>
    [HttpGet("stock-items/below-minimum")]
    [ProducesResponseType(typeof(ApiResult<PagedResult<BelowMinimumItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListItemsBelowMinimum(
        [FromQuery] Guid? storageLocationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<BelowMinimumItem> result = await _mediator.SendAsync(
            new ListItemsBelowMinimumQuery
            {
                StorageLocationId = storageLocationId,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<BelowMinimumItem>>(true, "Items below minimum retrieved.", result));
    }

    /// <summary>
    /// Returns the single "reposição" KPI for the active company: how many stock items are currently below
    /// their minimum stock. Feeds the dashboard KPI and the reposition-alert job without pulling the list.
    /// </summary>
    [HttpGet("stock-items/below-minimum/summary")]
    [ProducesResponseType(typeof(ApiResult<BelowMinimumSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBelowMinimumSummary(CancellationToken ct)
    {
        BelowMinimumSummary summary = await _mediator.SendAsync(new GetBelowMinimumSummaryQuery(), ct);

        return Ok(new ApiResult<BelowMinimumSummary>(true, "Below-minimum summary retrieved.", summary));
    }

    /// <summary>
    /// Returns the active company's consumption report over <paramref name="from"/>..<paramref name="to"/>:
    /// per-item consumed amounts (aggregated per unit, joined with the item's name and category), optionally
    /// narrowed to one experiment and/or category, paginated, plus the per-unit grand totals over the whole
    /// period.
    /// </summary>
    [HttpGet("consumption-report")]
    [ProducesResponseType(typeof(ApiResult<ConsumptionReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetConsumptionReport(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? experimentId = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        ConsumptionReport report = await _mediator.SendAsync(
            new GetConsumptionReportQuery
            {
                From = from,
                To = to,
                ExperimentId = experimentId,
                Category = category,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<ConsumptionReport>(true, "Consumption report retrieved.", report));
    }

    /// <summary>
    /// Returns the active company's consumption time series over <paramref name="from"/>..<paramref name="to"/>:
    /// total consumption bucketed by day or month (derived from the window when <paramref name="bucket"/> is
    /// omitted), optionally narrowed to one experiment, plus the per-unit period totals and the % delta versus
    /// the same-length preceding period. Feeds the dashboard chart.
    /// </summary>
    [HttpGet("consumption-series")]
    [ProducesResponseType(typeof(ApiResult<ConsumptionSeries>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetConsumptionSeries(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] ConsumptionBucket? bucket = null,
        [FromQuery] Guid? experimentId = null,
        CancellationToken ct = default)
    {
        ConsumptionSeries series = await _mediator.SendAsync(
            new GetConsumptionSeriesQuery
            {
                From = from,
                To = to,
                Bucket = bucket,
                ExperimentId = experimentId
            },
            ct);

        return Ok(new ApiResult<ConsumptionSeries>(true, "Consumption series retrieved.", series));
    }
}
