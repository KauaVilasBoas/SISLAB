using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Partners;

/// <summary>
/// HTTP boundary for the partners (suppliers/clients) of the <b>active company</b>: registration,
/// update, activation/deactivation and the incremental recording of samples. The controller only
/// dispatches CQRS commands through <see cref="IMediator"/> and maps the successful result to the
/// uniform <see cref="ApiResult"/> envelope; it never touches repositories, the DbContext or Dapper,
/// and never maps errors — those bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every command runs against the active company resolved from the httpOnly cookie
/// (EF Core global query filter + <c>ITenantContext</c>), never from the request body.
/// </remarks>
[Route("api/inventory/partners")]
[Authorize]
public sealed class PartnersController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public PartnersController(IMediator mediator) => _mediator = mediator;

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
