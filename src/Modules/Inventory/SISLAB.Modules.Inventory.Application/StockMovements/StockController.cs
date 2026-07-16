using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Inventory.Application.StockMovements.Commands;
using SISLAB.Modules.Inventory.Application.StockMovements.Queries;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StockMovements;

/// <summary>
/// HTTP boundary for the stock of the <b>active company</b>. It groups both sides of the inventory experience
/// behind a single controller: the write side — stock movements (entry, consumption, transfer, disposal and
/// physical count/conference) — and the read side — the master-detail listing (#46), the per-location summary,
/// the expiry list and donut (#49), the low-stock list and KPI, and the consumption report/series. The controller
/// only dispatches CQRS requests through <see cref="IMediator"/> and maps the successful result to the uniform
/// <see cref="ApiResult"/>/<see cref="ApiResult{T}"/> envelope; it never touches repositories, the DbContext or
/// Dapper, and never maps errors — those bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie
/// (EF Core global query filter + <c>ITenantContext</c>; on the read side the query handlers keep the mandatory
/// <c>WHERE company_id = @CompanyId</c>), never from the request body. The operator (responsável) is likewise the
/// authenticated user, captured by the audit trail (card #57) — never accepted in the payload (decision recorded
/// on card [E3] #24).
/// </remarks>
[Route("api/inventory")]
[Authorize]
public sealed class StockController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public StockController(IMediator mediator) => _mediator = mediator;

    // ---- Read side -------------------------------------------------------------------------------------------

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
        [FromQuery] bool? isControlled = null,
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
                IsControlled = isControlled,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<StockItemListItem>>(true, "Stock items retrieved.", result));
    }

    /// <summary>
    /// Lists the movement history (ledger) of a single stock item of the active company — entries,
    /// consumptions, transfers and disposals — most recent first, optionally narrowed by movement type
    /// and/or a business-date window, paginated. Gated by <c>Stock.ListStockMovements</c>.
    /// </summary>
    [HttpGet("stock-items/{stockItemId:guid}/movements")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<PagedResult<StockMovementListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListStockMovements(
        Guid stockItemId,
        [FromQuery] string? type = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<StockMovementListItem> result = await _mediator.SendAsync(
            new ListStockMovementsQuery
            {
                StockItemId = stockItemId,
                Type = type,
                From = from,
                To = to,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<StockMovementListItem>>(true, "Stock movements retrieved.", result));
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

    // ---- Write side ------------------------------------------------------------------------------------------

    /// <summary>
    /// Registers a new stock item for the active company with its initial balance. The category is a per-tenant
    /// Configuration category referenced by value (card [E12] #76); an unknown category id — or an unknown
    /// storage location — is a 404. Returns the new item id.
    /// </summary>
    [HttpPost("stock-items")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterStockItem(
        [FromBody] RegisterStockItemRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new RegisterStockItemCommand(
                body.Name,
                body.CategoryId,
                body.StorageLocationId,
                body.InitialQuantity,
                body.MinimumQuantity,
                body.Unit,
                body.IsControlled,
                body.Brand,
                body.Application,
                body.LotCode,
                body.ExpiryYear,
                body.ExpiryMonth),
            ct);

        return Ok(new ApiResult<Guid>(true, "Stock item registered.", id));
    }

    /// <summary>Registers an incoming stock entry (receipt) on an existing item.</summary>
    [HttpPost("stock-items/{stockItemId:guid}/entries")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterEntry(
        Guid stockItemId,
        [FromBody] RegisterStockEntryRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new RegisterStockEntryCommand(
                stockItemId,
                body.Quantity,
                body.Unit,
                body.LotCode,
                body.ExpiryYear,
                body.ExpiryMonth,
                body.SupplierPartnerId,
                body.OccurredOn),
            ct);

        return Ok(new ApiResult<Guid>(true, "Stock entry registered.", id));
    }

    /// <summary>Registers a consumption, decreasing the item balance.</summary>
    [HttpPost("stock-items/{stockItemId:guid}/consumptions")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterConsumption(
        Guid stockItemId,
        [FromBody] RegisterConsumptionRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new RegisterConsumptionCommand(
                stockItemId,
                body.Quantity,
                body.Unit,
                body.ExperimentId,
                body.OccurredOn),
            ct);

        return Ok(new ApiResult(true, "Consumption registered."));
    }

    /// <summary>Transfers the item to another storage location.</summary>
    [HttpPost("stock-items/{stockItemId:guid}/transfers")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Transfer(
        Guid stockItemId,
        [FromBody] TransferStockRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new TransferStockCommand(
                stockItemId,
                body.FromLocationId,
                body.ToLocationId,
                body.OccurredOn),
            ct);

        return Ok(new ApiResult(true, "Stock transferred."));
    }

    /// <summary>Discards a quantity of stock (auditable, especially for controlled items).</summary>
    [HttpPost("stock-items/{stockItemId:guid}/disposals")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Dispose(
        Guid stockItemId,
        [FromBody] DisposeStockRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new DisposeStockCommand(
                stockItemId,
                body.Quantity,
                body.Unit,
                body.Reason,
                body.OccurredOn),
            ct);

        return Ok(new ApiResult(true, "Stock disposed."));
    }

    /// <summary>
    /// Records a physical stock count (conference) of a controlled item, returning the divergence
    /// (counted minus system balance). Does not change the balance.
    /// </summary>
    [HttpPost("stock-items/{stockItemId:guid}/counts")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<decimal>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterCount(
        Guid stockItemId,
        [FromBody] RegisterStockCountRequest body,
        CancellationToken ct)
    {
        decimal divergence = await _mediator.SendAsync(
            new RegisterStockCountCommand(
                stockItemId,
                body.CountedQuantity,
                body.Unit,
                body.OccurredOn),
            ct);

        return Ok(new ApiResult<decimal>(true, "Stock count registered.", divergence));
    }
}

/// <summary>
/// Request body for registering a new stock item. <paramref name="CategoryId"/> is a per-tenant Configuration
/// category referenced by value (card [E12] #76); the operator comes from the session, never the payload.
/// </summary>
public sealed record RegisterStockItemRequest(
    string Name,
    Guid CategoryId,
    Guid StorageLocationId,
    decimal InitialQuantity,
    decimal MinimumQuantity,
    string Unit,
    bool IsControlled,
    string? Brand,
    string? Application,
    string? LotCode,
    int? ExpiryYear,
    int? ExpiryMonth);

/// <summary>Request body for a stock entry; the item id comes from the route, the operator from the session.</summary>
public sealed record RegisterStockEntryRequest(
    decimal Quantity,
    string Unit,
    string? LotCode,
    int? ExpiryYear,
    int? ExpiryMonth,
    Guid? SupplierPartnerId,
    DateOnly? OccurredOn);

/// <summary>Request body for a consumption; <paramref name="ExperimentId"/> is an optional by-value reference.</summary>
public sealed record RegisterConsumptionRequest(
    decimal Quantity,
    string Unit,
    Guid? ExperimentId,
    DateOnly? OccurredOn);

/// <summary>Request body for a transfer between storage locations.</summary>
public sealed record TransferStockRequest(
    Guid FromLocationId,
    Guid ToLocationId,
    DateOnly? OccurredOn);

/// <summary>Request body for a disposal; <paramref name="Reason"/> is the audited justification.</summary>
public sealed record DisposeStockRequest(
    decimal Quantity,
    string Unit,
    string Reason,
    DateOnly? OccurredOn);

/// <summary>Request body for a physical count (conference) of a controlled item.</summary>
public sealed record RegisterStockCountRequest(
    decimal CountedQuantity,
    string Unit,
    DateOnly? OccurredOn);
