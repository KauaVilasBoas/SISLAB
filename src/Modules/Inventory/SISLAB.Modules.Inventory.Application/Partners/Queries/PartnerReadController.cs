using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners.Queries;

/// <summary>
/// Read-side (CQRS query) HTTP boundary for the "Parceiros" screen (#48) of the <b>active company</b>: the
/// paginated partner listing (filterable by type and free-text search) and the per-partner detail. The controller
/// only dispatches queries through <see cref="IMediator"/> and wraps the result in the uniform
/// <see cref="ApiResult{T}"/> envelope; it never touches Dapper, the DbContext or repositories. A missing detail is
/// translated into a <see cref="NotFoundException"/> (404) here, keeping the query pure and reusable; every other
/// error bubbles up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: the active company is resolved from the httpOnly cookie into <c>ITenantContext</c> and read by
/// the query handlers (which keep the mandatory <c>WHERE company_id = @CompanyId</c>), never from the request.
/// Kept separate from the write-side <c>PartnersController</c> to honour CQRS (reads and writes have independent
/// handlers and contracts) and to avoid an MVC controller-name collision — mirroring the StockRead slice.
/// </remarks>
[Route("api/inventory/partners")]
[Authorize]
public sealed class PartnerReadController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public PartnerReadController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lists the active company's partners for the partners table, optionally filtered by type and free-text
    /// search, ordered by name and paginated. Inactive (deactivated) partners are hidden unless
    /// <paramref name="includeInactive"/> is set.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PagedResult<PartnerListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListPartners(
        [FromQuery] PartnerType? type,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<PartnerListItem> result = await _mediator.SendAsync(
            new ListPartnersQuery
            {
                Type = type,
                Search = search,
                IncludeInactive = includeInactive,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<PartnerListItem>>(true, "Partners retrieved.", result));
    }

    /// <summary>Returns the detail of a single partner of the active company, or 404 when it does not exist.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<PartnerDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPartnerDetail(Guid id, CancellationToken ct)
    {
        PartnerDetail detail = await _mediator.SendAsync(new GetPartnerDetailQuery(id), ct)
            ?? throw new NotFoundException("Partner", id);

        return Ok(new ApiResult<PartnerDetail>(true, "Partner retrieved.", detail));
    }
}
