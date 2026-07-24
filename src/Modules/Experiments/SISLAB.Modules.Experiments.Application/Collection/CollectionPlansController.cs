using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Experiments.Application.Collection.Commands;
using SISLAB.Modules.Experiments.Application.Collection.Queries;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Collection;

/// <summary>
/// HTTP boundary for the collection plan of a batch (SISLAB-08): create the plan, define its matrix (sample type →
/// planned analyses + storage), assign collection roles to members, and read back the plan and its derived status board.
/// The controller only dispatches CQRS requests through <see cref="IMediator"/> and maps the result to the uniform
/// <see cref="ApiResult"/>/<see cref="ApiResult{T}"/> envelope; it never touches repositories, the DbContext or Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from the
/// request body. Each state-changing action is gated by Lumen's <c>[RequirePermission]</c>; the reads are page-level
/// <c>[Authorize]</c>.
/// </remarks>
[Route("api/collection-plans")]
[Authorize]
public sealed class CollectionPlansController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public CollectionPlansController(IMediator mediator) => _mediator = mediator;

    /// <summary>Returns a batch's collection plan — its matrix and role roster.</summary>
    [HttpGet("{batchId:guid}")]
    [ProducesResponseType(typeof(ApiResult<CollectionPlanView>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid batchId, CancellationToken ct)
    {
        CollectionPlanView view = await _mediator.SendAsync(new GetCollectionPlanQuery(batchId), ct);
        return Ok(new ApiResult<CollectionPlanView>(true, "Collection plan retrieved.", view));
    }

    /// <summary>Returns a batch's collection status board — pending/done per planned analysis, derived from the biobank.</summary>
    [HttpGet("{batchId:guid}/status")]
    [ProducesResponseType(typeof(ApiResult<CollectionStatusBoardView>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid batchId, CancellationToken ct)
    {
        CollectionStatusBoardView view = await _mediator.SendAsync(new GetCollectionStatusBoardQuery(batchId), ct);
        return Ok(new ApiResult<CollectionStatusBoardView>(true, "Collection status board retrieved.", view));
    }

    /// <summary>Creates the collection plan for a batch. Returns the new plan id.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateCollectionPlanRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(new CreateCollectionPlanCommand(body.ProjectId, body.BatchId), ct);
        return Ok(new ApiResult<Guid>(true, "Collection plan created.", id));
    }

    /// <summary>Defines (or replaces) a sample type's routing on the plan.</summary>
    [HttpPut("{planId:guid}/routings")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefineRouting(
        Guid planId,
        [FromBody] DefineSampleRoutingRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new DefineSampleRoutingCommand(
                planId,
                body.SampleType,
                body.PlannedAnalyses,
                body.StorageRoomId,
                body.StorageLabel,
                body.ConservationTempMinCelsius,
                body.ConservationTempMaxCelsius),
            ct);

        return Ok(new ApiResult(true, "Sample routing defined."));
    }

    /// <summary>Removes a sample type's routing from the plan.</summary>
    [HttpDelete("{planId:guid}/routings/{sampleType}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRouting(Guid planId, SampleType sampleType, CancellationToken ct)
    {
        await _mediator.SendAsync(new RemoveSampleRoutingCommand(planId, sampleType), ct);
        return Ok(new ApiResult(true, "Sample routing removed."));
    }

    /// <summary>Assigns a member to a collection role on the plan.</summary>
    [HttpPut("{planId:guid}/roles")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignRole(
        Guid planId,
        [FromBody] AssignCollectionRoleRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(new AssignCollectionRoleCommand(planId, body.RoleId, body.UserId), ct);
        return Ok(new ApiResult(true, "Collection role assigned."));
    }

    /// <summary>Removes a role assignment from the plan.</summary>
    [HttpDelete("{planId:guid}/roles/{roleId:guid}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(Guid planId, Guid roleId, CancellationToken ct)
    {
        await _mediator.SendAsync(new RemoveCollectionRoleAssignmentCommand(planId, roleId), ct);
        return Ok(new ApiResult(true, "Collection role assignment removed."));
    }
}

/// <summary>Request body to create a collection plan for a batch.</summary>
public sealed record CreateCollectionPlanRequest(Guid ProjectId, Guid BatchId);

/// <summary>Request body to define a sample type's routing (planned analyses + storage).</summary>
public sealed record DefineSampleRoutingRequest(
    SampleType SampleType,
    IReadOnlyList<string> PlannedAnalyses,
    Guid? StorageRoomId,
    string? StorageLabel,
    decimal? ConservationTempMinCelsius,
    decimal? ConservationTempMaxCelsius);

/// <summary>Request body to assign a member to a collection role.</summary>
public sealed record AssignCollectionRoleRequest(Guid RoleId, Guid UserId);
