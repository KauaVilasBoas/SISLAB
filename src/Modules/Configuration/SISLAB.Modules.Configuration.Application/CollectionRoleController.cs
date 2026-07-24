using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.CollectionRoles;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's collection roles (SISLAB-08): listing and creating the configurable jobs
/// (Volante, Anestesia, Sangue, …) a lab assigns to people on a collection sheet. The controller only dispatches CQRS
/// requests through <see cref="IMediator"/> and maps the result to the uniform <see cref="ApiResult"/> envelope; it
/// never touches repositories, the DbContext or Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from the
/// request body. Each state-changing action is gated by Lumen's <c>[RequirePermission]</c>; the read is page-level
/// <c>[Authorize]</c>.
/// </remarks>
[Route("api/configuration/collection-roles")]
[Authorize]
public sealed class CollectionRoleController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public CollectionRoleController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's collection roles, ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<CollectionRoleListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<CollectionRoleListItem> roles = await _mediator.SendAsync(new ListCollectionRolesQuery(), ct);

        return Ok(new ApiResult<IReadOnlyList<CollectionRoleListItem>>(true, "Collection roles listed.", roles));
    }

    /// <summary>Creates a new collection role for the active company.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateCollectionRoleRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(new CreateCollectionRoleCommand(body.Name, body.Description), ct);

        return Ok(new ApiResult<Guid>(true, "Collection role created.", id));
    }
}

/// <summary>Request body for creating a collection role.</summary>
public sealed record CreateCollectionRoleRequest(string Name, string? Description);
