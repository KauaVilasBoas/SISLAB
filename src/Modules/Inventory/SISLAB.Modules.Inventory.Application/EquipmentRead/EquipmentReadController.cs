using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.EquipmentRead;

/// <summary>
/// Read-side (CQRS query) HTTP boundary for the "Equipamentos" screen (#48) of the <b>active company</b>: the
/// paginated equipment listing (filterable by calibration status, storage location and free-text search) and the
/// per-equipment detail. The controller only dispatches queries through <see cref="IMediator"/> and wraps the
/// result in the uniform <see cref="ApiResult{T}"/> envelope; it never touches Dapper, the DbContext or
/// repositories. A missing detail is translated into a <see cref="NotFoundException"/> (404) here, keeping the
/// query pure and reusable; every other error bubbles up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: the active company is resolved from the httpOnly cookie into <c>ITenantContext</c> and read by
/// the query handlers (which keep the mandatory <c>WHERE company_id = @CompanyId</c>), never from the request.
/// Kept separate from the write-side <c>EquipmentController</c> to honour CQRS (reads and writes have independent
/// handlers and contracts) and to avoid an MVC controller-name collision — mirroring the StockRead slice.
/// </remarks>
[Route("api/inventory/equipment")]
[Authorize]
public sealed class EquipmentReadController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public EquipmentReadController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lists the active company's equipment for the equipment table, optionally filtered by calibration status,
    /// storage location and free-text search, ordered by name and paginated. Inactive (retired) equipment is
    /// hidden unless <paramref name="includeInactive"/> is set.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PagedResult<EquipmentListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListEquipment(
        [FromQuery] CalibrationStatus? status,
        [FromQuery] Guid? storageLocationId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<EquipmentListItem> result = await _mediator.SendAsync(
            new ListEquipmentQuery
            {
                Status = status,
                StorageLocationId = storageLocationId,
                Search = search,
                IncludeInactive = includeInactive,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<EquipmentListItem>>(true, "Equipment retrieved.", result));
    }

    /// <summary>Returns the detail of a single equipment of the active company, or 404 when it does not exist.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<EquipmentDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEquipmentDetail(Guid id, CancellationToken ct)
    {
        EquipmentDetail detail = await _mediator.SendAsync(new GetEquipmentDetailQuery(id), ct)
            ?? throw new NotFoundException("Equipment", id);

        return Ok(new ApiResult<EquipmentDetail>(true, "Equipment retrieved.", detail));
    }
}
