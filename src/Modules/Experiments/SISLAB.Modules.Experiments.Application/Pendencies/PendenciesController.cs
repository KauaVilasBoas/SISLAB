using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Experiments.Application.Pendencies.Queries;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Pendencies;

/// <summary>
/// HTTP boundary for the Experiments module's pendencies panel (card [E11] #90): the read-only dashboard of open
/// work — experiments awaiting calculation, unperformed steps and biobank samples still awaiting analysis. The
/// controller only dispatches the CQRS query through <see cref="IMediator"/> and wraps the result in the uniform
/// <see cref="ApiResult{T}"/> envelope.
/// </summary>
/// <remarks>
/// It is a read of the active company's own tables, so it is page-level <c>[Authorize]</c> — any authenticated
/// member may see the panel — not permission-gated. Tenant scoping is enforced by the query's mandatory
/// <c>WHERE company_id = @CompanyId</c>, taken from the session, never the request.
/// </remarks>
[Route("api/experiments/pendencies")]
[Authorize]
public sealed class PendenciesController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public PendenciesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Returns the active company's open pendencies with per-kind summary counts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PendenciesResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        PendenciesResult result = await _mediator.SendAsync(new GetPendenciesQuery(), ct);
        return Ok(new ApiResult<PendenciesResult>(true, "Pendencies retrieved.", result));
    }
}
