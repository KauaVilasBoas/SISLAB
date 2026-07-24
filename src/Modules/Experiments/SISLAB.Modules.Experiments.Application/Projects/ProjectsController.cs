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

    /// <summary>Adds a cage (caixa) to a batch (SISLAB-03). Capacity (e.g. 4) is a parameter. Returns the new id.</summary>
    [HttpPost("{projectId:guid}/batches/{batchId:guid}/cages")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddCage(
        Guid projectId,
        Guid batchId,
        [FromBody] AddCageRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(new AddCageCommand(projectId, batchId, body.Name, body.Capacity), ct);
        return Ok(new ApiResult<Guid>(true, "Cage added.", id));
    }

    /// <summary>
    /// Houses an animal in a cage of a batch (SISLAB-03). The treatment group is optional: omit it for the
    /// pre-randomization flow (assign later), or supply it to assign at entry. Returns the new id.
    /// </summary>
    [HttpPost("{projectId:guid}/batches/{batchId:guid}/cages/{cageId:guid}/animals")]
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
        Guid cageId,
        [FromBody] AddAnimalRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new AddAnimalCommand(projectId, batchId, cageId, body.Identifier, body.Sex, body.WeightGrams, body.GroupId),
            ct);

        return Ok(new ApiResult<Guid>(true, "Animal housed.", id));
    }

    /// <summary>
    /// Assigns (or moves) an animal to a treatment group after basal/induction (SISLAB-03) — including redistributing a
    /// discrepant cage. Locked once the batch starts (frozen design).
    /// </summary>
    [HttpPut("{projectId:guid}/batches/{batchId:guid}/animals/{animalId:guid}/group")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignAnimalToGroup(
        Guid projectId,
        Guid batchId,
        Guid animalId,
        [FromBody] AssignAnimalToGroupRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new AssignAnimalToGroupCommand(projectId, batchId, animalId, body.GroupId), ct);

        return Ok(new ApiResult(true, "Animal assigned to group."));
    }

    /// <summary>
    /// Records a physiological reading (glicemia/peso, … — SISLAB-02) on a project animal at a timepoint. The author
    /// and instant come from the session/clock; the parameter code and unit are cadaster values. Returns the new id.
    /// </summary>
    [HttpPost("{projectId:guid}/animals/{animalId:guid}/readings")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordReading(
        Guid projectId,
        Guid animalId,
        [FromBody] RecordReadingRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new RecordPhysiologicalReadingCommand(
                projectId, animalId, body.ParameterCode, body.Value, body.Unit, body.TimepointLabel),
            ct);

        return Ok(new ApiResult<Guid>(true, "Physiological reading recorded.", id));
    }

    /// <summary>Lists a project's physiological readings, optionally filtered by parameter code and/or animal.</summary>
    [HttpGet("{projectId:guid}/readings")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<PhysiologicalReadingListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListReadings(
        Guid projectId,
        [FromQuery] string? parameterCode = null,
        [FromQuery] Guid? animalId = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<PhysiologicalReadingListItem> readings = await _mediator.SendAsync(
            new ListPhysiologicalReadingsQuery(projectId) { ParameterCode = parameterCode, AnimalId = animalId },
            ct);

        return Ok(new ApiResult<IReadOnlyList<PhysiologicalReadingListItem>>(true, "Readings retrieved.", readings));
    }

    /// <summary>
    /// Applies the inclusion criteria (SISLAB-02) to a batch's animals, marking each included/excluded from its
    /// physiological readings. The batch's experimental model gates which parameters apply. Returns how many animals
    /// a decision was taken for.
    /// </summary>
    [HttpPost("{projectId:guid}/batches/{batchId:guid}/apply-selection")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ApplySelection(Guid projectId, Guid batchId, CancellationToken ct)
    {
        int decided = await _mediator.SendAsync(new ApplyInclusionCriteriaCommand(projectId, batchId), ct);
        return Ok(new ApiResult<int>(true, "Inclusion criteria applied.", decided));
    }

    /// <summary>Lists a batch's animals with their inclusion decision, optionally filtered by inclusion status.</summary>
    [HttpGet("{projectId:guid}/batches/{batchId:guid}/selection")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<AnimalSelectionListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListSelection(
        Guid projectId,
        Guid batchId,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<AnimalSelectionListItem> selection = await _mediator.SendAsync(
            new ListAnimalSelectionQuery(projectId, batchId) { Status = status }, ct);

        return Ok(new ApiResult<IReadOnlyList<AnimalSelectionListItem>>(true, "Selection retrieved.", selection));
    }

    /// <summary>
    /// Summarizes a batch's readings of one parameter <b>by cage</b> (SISLAB-03) — the pre-randomization basal view the
    /// researcher exports to Prism. Optionally narrowed to a single timepoint.
    /// </summary>
    [HttpGet("{projectId:guid}/batches/{batchId:guid}/baseline/by-cage")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<CageBaselineItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BaselineByCage(
        Guid projectId,
        Guid batchId,
        [FromQuery] string parameterCode,
        [FromQuery] string? timepointLabel = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<CageBaselineItem> baseline = await _mediator.SendAsync(
            new ListBaselineByCageQuery(projectId, batchId, parameterCode) { TimepointLabel = timepointLabel }, ct);

        return Ok(new ApiResult<IReadOnlyList<CageBaselineItem>>(true, "Baseline by cage retrieved.", baseline));
    }

    /// <summary>
    /// Summarizes a batch's readings of one parameter <b>by treatment group</b> (SISLAB-03) — the post-randomization
    /// balance-check view ("reordena o basal por grupo"). Optionally narrowed to a single timepoint.
    /// </summary>
    [HttpGet("{projectId:guid}/batches/{batchId:guid}/baseline/by-group")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<GroupBaselineItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BaselineByGroup(
        Guid projectId,
        Guid batchId,
        [FromQuery] string parameterCode,
        [FromQuery] string? timepointLabel = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<GroupBaselineItem> baseline = await _mediator.SendAsync(
            new ListBaselineByGroupQuery(projectId, batchId, parameterCode) { TimepointLabel = timepointLabel }, ct);

        return Ok(new ApiResult<IReadOnlyList<GroupBaselineItem>>(true, "Baseline by group retrieved.", baseline));
    }

    /// <summary>
    /// Binds a batch (leva) to an experimental model (SISLAB-04). The model is validated to exist for the active
    /// company through the Configuration Contracts port; the batch keeps only the model id, by value.
    /// </summary>
    [HttpPut("{projectId:guid}/batches/{batchId:guid}/model")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BindBatchModel(
        Guid projectId,
        Guid batchId,
        [FromBody] BindBatchModelRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new BindBatchToModelCommand(projectId, batchId, body.ExperimentalModelId), ct);

        return Ok(new ApiResult(true, "Batch bound to experimental model."));
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

/// <summary>Request body to bind a batch to an experimental model (SISLAB-04); the model id is referenced by value.</summary>
public sealed record BindBatchModelRequest(Guid ExperimentalModelId);

/// <summary>Request body to add a dose group to a batch.</summary>
public sealed record AddGroupRequest(string Name, decimal DoseAmount, string DoseUnit);

/// <summary>Request body to add a cage (caixa) to a batch (SISLAB-03); capacity (e.g. 4) is a parameter.</summary>
public sealed record AddCageRequest(string Name, int? Capacity);

/// <summary>
/// Request body to house an animal in a cage (SISLAB-03). <see cref="GroupId"/> is optional: null houses the animal
/// unassigned (pre-randomization); a value assigns the group at entry.
/// </summary>
public sealed record AddAnimalRequest(string Identifier, AnimalSex Sex, decimal? WeightGrams, Guid? GroupId = null);

/// <summary>Request body to assign/move an animal to a treatment group (SISLAB-03).</summary>
public sealed record AssignAnimalToGroupRequest(Guid GroupId);

/// <summary>Request body to record a physiological reading (SISLAB-02); author/instant come from the session/clock.</summary>
public sealed record RecordReadingRequest(string ParameterCode, decimal Value, string Unit, string TimepointLabel);
