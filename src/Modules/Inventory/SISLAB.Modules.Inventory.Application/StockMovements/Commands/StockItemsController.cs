using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StockMovements.Commands;

/// <summary>
/// HTTP boundary for stock movements of the <b>active company</b>: entry, consumption, transfer,
/// disposal and physical count (conference). The controller only dispatches CQRS commands through
/// <see cref="IMediator"/> and maps the successful result to the uniform <see cref="ApiResult"/>
/// envelope; it never touches repositories, the DbContext or Dapper, and never maps errors — those
/// bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every command runs against the active company resolved from the httpOnly cookie
/// (EF Core global query filter + <c>ITenantContext</c>), never from the request body. The operator
/// (responsável) is likewise the authenticated user, captured by the audit trail (card #57) — it is
/// never accepted in the payload (decision recorded on card [E3] #24).
/// </remarks>
[Route("api/inventory/stock-items")]
[Authorize]
public sealed class StockItemsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public StockItemsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Registers an incoming stock entry (receipt) on an existing item.</summary>
    [HttpPost("{stockItemId:guid}/entries")]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [HttpPost("{stockItemId:guid}/consumptions")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [HttpPost("{stockItemId:guid}/transfers")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [HttpPost("{stockItemId:guid}/disposals")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [HttpPost("{stockItemId:guid}/counts")]
    [ProducesResponseType(typeof(ApiResult<decimal>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
