using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.ExperimentalModels;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's experimental models (SISLAB-04, sub-task 1): listing, resolving and
/// cadastering them. The controller only dispatches CQRS requests through <see cref="IMediator"/> and maps the
/// result to the uniform <see cref="ApiResult"/> envelope; it never touches repositories, the DbContext or Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from the
/// request body. The write endpoint is permission-gated by Lumen's <see cref="RequirePermissionAttribute"/>.
/// </remarks>
[Route("api/configuration/experimental-models")]
[Authorize]
public sealed class ExperimentalModelController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public ExperimentalModelController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's experimental models, ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<ExperimentalModelListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<ExperimentalModelListItem> models =
            await _mediator.SendAsync(new ListExperimentalModelsQuery(), ct);

        return Ok(new ApiResult<IReadOnlyList<ExperimentalModelListItem>>(true, "Experimental models listed.", models));
    }

    /// <summary>Resolves one experimental model of the active company by id, with its full payload.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<ExperimentalModelView>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        ExperimentalModelView? model = await _mediator.SendAsync(new GetExperimentalModelQuery(id), ct);

        if (model is null)
            return NotFound(new ApiResult<ExperimentalModelView>(false, "Experimental model not found.", null));

        return Ok(new ApiResult<ExperimentalModelView>(true, "Experimental model resolved.", model));
    }

    /// <summary>Cadasters a new experimental model for the active company.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateExperimentalModelCommand body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(body, ct);

        return Ok(new ApiResult<Guid>(true, "Experimental model created.", id));
    }
}
