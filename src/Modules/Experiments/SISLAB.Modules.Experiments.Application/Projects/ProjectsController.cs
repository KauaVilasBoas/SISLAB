using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Experiments.Application.Projects.Commands;
using SISLAB.Modules.Experiments.Application.Projects.Queries;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects;

/// <summary>
/// HTTP boundary for the in vivo experimental design (card [E11] #73, decision F1 — <c>Project → Batch → Group →
/// Animal</c>). It groups the write side (create project → add batch → add group → add animal → start batch) and
/// the read side (list / detail). The controller only dispatches CQRS requests through <see cref="IMediator"/> and
/// maps the result to the uniform <see cref="ApiResult"/>/<see cref="ApiResult{T}"/> envelope; it never touches
/// repositories, the DbContext or Dapper, and never maps errors — those bubble up to the exception middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie (EF Core global
/// query filter + <c>ITenantContext</c> on the write side; the read side keeps the mandatory
/// <c>WHERE company_id = @CompanyId</c>), never from the request body. Each state-changing action is gated by
/// Lumen's <c>[RequirePermission]</c>; the reads are page-level <c>[Authorize]</c>.
/// </remarks>
[Route("api/projects")]
[Authorize]
public sealed class ProjectsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's in vivo projects, paginated, optionally filtered by status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PagedResult<ProjectListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        PagedResult<ProjectListItem> result = await _mediator.SendAsync(
            new ListProjectsQuery { Page = page, PageSize = pageSize, Status = status },
            ct);

        return Ok(new ApiResult<PagedResult<ProjectListItem>>(true, "Projects retrieved.", result));
    }

    /// <summary>Returns a single project's full delineation — header, batches, groups and animals.</summary>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResult<ProjectDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        ProjectDetail detail = await _mediator.SendAsync(new GetProjectQuery(projectId), ct);
        return Ok(new ApiResult<ProjectDetail>(true, "Project retrieved.", detail));
    }

    /// <summary>Creates a new in vivo project for the active company. Returns the new id.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateProjectCommand(body.Name, body.Species, body.Description), ct);

        return Ok(new ApiResult<Guid>(true, "Project created.", id));
    }

    /// <summary>Adds a batch (leva) to the project, pinned to the current design version. Returns the new id.</summary>
    [HttpPost("{projectId:guid}/batches")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddBatch(
        Guid projectId,
        [FromBody] AddBatchRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(new AddBatchCommand(projectId, body.Name), ct);
        return Ok(new ApiResult<Guid>(true, "Batch added.", id));
    }

    /// <summary>Adds a dose group (treatment arm) to a batch of the project. Returns the new id.</summary>
    [HttpPost("{projectId:guid}/batches/{batchId:guid}/groups")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddGroup(
        Guid projectId,
        Guid batchId,
        [FromBody] AddGroupRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new AddGroupCommand(projectId, batchId, body.Name, body.DoseAmount, body.DoseUnit), ct);

        return Ok(new ApiResult<Guid>(true, "Group added.", id));
    }

    /// <summary>Enrols an animal into a group of a batch. Returns the new id.</summary>
    [HttpPost("{projectId:guid}/batches/{batchId:guid}/groups/{groupId:guid}/animals")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddAnimal(
        Guid projectId,
        Guid batchId,
        Guid groupId,
        [FromBody] AddAnimalRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new AddAnimalCommand(projectId, batchId, groupId, body.Identifier, body.Sex, body.WeightGrams), ct);

        return Ok(new ApiResult<Guid>(true, "Animal enrolled.", id));
    }

    /// <summary>Starts a batch: freezes its design (reproducible cohort) and activates the project.</summary>
    [HttpPost("{projectId:guid}/batches/{batchId:guid}/start")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StartBatch(Guid projectId, Guid batchId, CancellationToken ct)
    {
        await _mediator.SendAsync(new StartBatchCommand(projectId, batchId), ct);
        return Ok(new ApiResult(true, "Batch started."));
    }
}

/// <summary>Request body to create a project; the company comes from the session, never the payload.</summary>
public sealed record CreateProjectRequest(string Name, string Species, string? Description);

/// <summary>Request body to add a batch (leva) to a project.</summary>
public sealed record AddBatchRequest(string Name);

/// <summary>Request body to add a dose group to a batch.</summary>
public sealed record AddGroupRequest(string Name, decimal DoseAmount, string DoseUnit);

/// <summary>Request body to enrol an animal into a group.</summary>
public sealed record AddAnimalRequest(string Identifier, AnimalSex Sex, decimal? WeightGrams);
