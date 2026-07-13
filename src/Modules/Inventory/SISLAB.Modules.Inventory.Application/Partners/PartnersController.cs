using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Inventory.Application.Partners.Commands;
using SISLAB.Modules.Inventory.Application.Partners.Queries;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners;

/// <summary>
/// HTTP boundary for the partners (suppliers/clients) of the <b>active company</b>. It groups both sides of the
/// "Parceiros" screen (#48) behind a single controller: the read side (paginated listing filterable by type and
/// free-text search, plus the per-partner detail) and the write side (registration, update, activation/deactivation
/// and the incremental recording of samples). The controller only dispatches CQRS requests through
/// <see cref="IMediator"/> and maps the successful result to the uniform <see cref="ApiResult"/>/
/// <see cref="ApiResult{T}"/> envelope; it never touches repositories, the DbContext or Dapper. A missing detail is
/// translated into a <see cref="NotFoundException"/> (404) here to keep the query pure; every other error bubbles up
/// to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie
/// (EF Core global query filter + <c>ITenantContext</c>; on the read side the query handlers keep the
/// mandatory <c>WHERE company_id = @CompanyId</c>), never from the request body.
/// </remarks>
[Route("api/inventory/partners")]
[Authorize]
public sealed class PartnersController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public PartnersController(IMediator mediator) => _mediator = mediator;

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

    /// <summary>Registers a new partner.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPartnerRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new RegisterPartnerCommand(
                body.Name,
                body.Type,
                body.Document,
                body.ContactEmail,
                body.Description),
            ct);

        return Ok(new ApiResult<Guid>(true, "Partner registered.", id));
    }

    /// <summary>Updates a partner's descriptive data.</summary>
    [HttpPut("{partnerId:guid}")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid partnerId,
        [FromBody] UpdatePartnerRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new UpdatePartnerCommand(
                partnerId,
                body.Name,
                body.Type,
                body.Document,
                body.ContactEmail,
                body.Description),
            ct);

        return Ok(new ApiResult(true, "Partner updated."));
    }

    /// <summary>Takes a partner out of service.</summary>
    [HttpPost("{partnerId:guid}/deactivation")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid partnerId, CancellationToken ct)
    {
        await _mediator.SendAsync(new DeactivatePartnerCommand(partnerId), ct);

        return Ok(new ApiResult(true, "Partner deactivated."));
    }

    /// <summary>Puts a deactivated partner back in service.</summary>
    [HttpPost("{partnerId:guid}/reactivation")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid partnerId, CancellationToken ct)
    {
        await _mediator.SendAsync(new ReactivatePartnerCommand(partnerId), ct);

        return Ok(new ApiResult(true, "Partner reactivated."));
    }

    /// <summary>Records a sample/compound the partner sent for testing.</summary>
    [HttpPost("{partnerId:guid}/samples")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordSample(
        Guid partnerId,
        [FromBody] RecordPartnerSampleRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new RecordPartnerSampleCommand(partnerId, body.Reference, body.Status),
            ct);

        return Ok(new ApiResult(true, "Sample recorded."));
    }

    /// <summary>Removes a previously recorded sample from a partner.</summary>
    [HttpDelete("{partnerId:guid}/samples/{reference}")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSample(
        Guid partnerId,
        string reference,
        CancellationToken ct)
    {
        await _mediator.SendAsync(new RemovePartnerSampleCommand(partnerId, reference), ct);

        return Ok(new ApiResult(true, "Sample removed."));
    }
}

/// <summary>Request body to register a partner; the company comes from the session, never the payload.</summary>
public sealed record RegisterPartnerRequest(
    string Name,
    PartnerType Type,
    string? Document,
    string? ContactEmail,
    string? Description);

/// <summary>Request body to update a partner's descriptive data.</summary>
public sealed record UpdatePartnerRequest(
    string Name,
    PartnerType Type,
    string? Document,
    string? ContactEmail,
    string? Description);

/// <summary>Request body to record a sample sent by the partner.</summary>
public sealed record RecordPartnerSampleRequest(
    string Reference,
    string? Status);
