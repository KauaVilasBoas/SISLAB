using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Experiments.Application.Experiments.Commands;
using SISLAB.Modules.Experiments.Application.Experiments.Queries;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Experiments;

/// <summary>
/// HTTP boundary for the Experiments module (decision card #68 — the in vitro viability slice). It groups the
/// write side (create → design plate → import reading → calculate) and the read side (list / detail / plate
/// result). The controller only dispatches CQRS requests through <see cref="IMediator"/> and maps the result to
/// the uniform <see cref="ApiResult"/>/<see cref="ApiResult{T}"/> envelope; it never touches repositories, the
/// DbContext or Dapper, and never maps errors — those bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie (EF Core
/// global query filter + <c>ITenantContext</c> on the write side; the read side keeps the mandatory
/// <c>WHERE company_id = @CompanyId</c>), never from the request body. Each state-changing action is gated by
/// Lumen's <c>[RequirePermission]</c>; the reads are page-level <c>[Authorize]</c>.
/// </remarks>
[Route("api/experiments")]
[Authorize]
public sealed class ExperimentsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public ExperimentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's experiments, paginated, optionally filtered by status/type.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PagedResult<ExperimentListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery(Name = "status")] string[]? status = null,
        [FromQuery] string? type = null,
        [FromQuery(Name = "responsibleId")] Guid[]? responsibleId = null,
        CancellationToken ct = default)
    {
        PagedResult<ExperimentListItem> result = await _mediator.SendAsync(
            new ListExperimentsQuery
            {
                Page = page,
                PageSize = pageSize,
                Statuses = status,
                Type = type,
                ResponsibleUserIds = responsibleId,
            },
            ct);

        return Ok(new ApiResult<PagedResult<ExperimentListItem>>(true, "Experiments retrieved.", result));
    }

    /// <summary>Returns a single experiment's detail — header, steps, plate wells and the calculation snapshot.</summary>
    [HttpGet("{experimentId:guid}")]
    [ProducesResponseType(typeof(ApiResult<ExperimentDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid experimentId, CancellationToken ct)
    {
        ExperimentDetail detail = await _mediator.SendAsync(new GetExperimentQuery(experimentId), ct);
        return Ok(new ApiResult<ExperimentDetail>(true, "Experiment retrieved.", detail));
    }

    /// <summary>Returns the experiment's plate as the 8×12 grid, with readings and (if calculated) % viability.</summary>
    [HttpGet("{experimentId:guid}/plate-result")]
    [ProducesResponseType(typeof(ApiResult<PlateReadingResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlateResult(Guid experimentId, CancellationToken ct)
    {
        PlateReadingResult result = await _mediator.SendAsync(new GetPlateReadingResultQuery(experimentId), ct);
        return Ok(new ApiResult<PlateReadingResult>(true, "Plate result retrieved.", result));
    }

    /// <summary>Creates a new plate experiment (viability or nitric oxide) for the active company. Returns the new id.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateExperimentRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateExperimentCommand(body.Type, body.Title, body.Description, body.CompoundPartnerId),
            ct);

        return Ok(new ApiResult<Guid>(true, "Experiment created.", id));
    }

    /// <summary>
    /// Creates a new in vivo behavioural experiment (von Frey / tail-flick / rota-rod / hemogram — card [E11] #88)
    /// bound to a project and batch, seeding its timepoint flow. Returns the new id.
    /// </summary>
    [HttpPost("behavioral")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateBehavioral(
        [FromBody] CreateBehavioralExperimentRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateBehavioralExperimentCommand(
                body.Type, body.Title, body.Description, body.ProjectId, body.BatchId, body.TimepointLabels),
            ct);

        return Ok(new ApiResult<Guid>(true, "Behavioural experiment created.", id));
    }

    /// <summary>
    /// Records the readings of a single timepoint on a behavioural experiment (one reading per animal — card
    /// [E11] #88) and advances the experiment to <c>AwaitingCalculation</c>.
    /// </summary>
    [HttpPost("{experimentId:guid}/timepoints")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordTimepoint(
        Guid experimentId,
        [FromBody] RecordTimepointRequest body,
        CancellationToken ct)
    {
        IReadOnlyList<TimepointReading> readings = body.Readings
            .Select(reading => new TimepointReading(reading.AnimalId, reading.RawValue))
            .ToList();

        await _mediator.SendAsync(new RecordTimepointCommand(experimentId, body.TimepointLabel, readings), ct);

        return Ok(new ApiResult(true, "Timepoint recorded."));
    }

    /// <summary>
    /// Runs the versioned calculation over a behavioural experiment's recorded timepoints (card [E11] #88) and
    /// freezes the result snapshot, advancing the experiment to <c>AwaitingAnalysis</c>.
    /// </summary>
    [HttpPost("{experimentId:guid}/calculate-behavioral")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CalculateBehavioral(Guid experimentId, CancellationToken ct)
    {
        await _mediator.SendAsync(new CalculateBehavioralExperimentCommand(experimentId), ct);
        return Ok(new ApiResult(true, "Behavioural calculation applied."));
    }

    /// <summary>Lays out the experiment's 8×12 plate (replaces the whole design). Moves a draft into progress.</summary>
    [HttpPost("{experimentId:guid}/design-plate")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DesignPlate(
        Guid experimentId,
        [FromBody] DesignPlateRequest body,
        CancellationToken ct)
    {
        IReadOnlyList<PlateWellDefinition> wells = body.Wells
            .Select(w => new PlateWellDefinition(w.Row, w.Column, w.Role, w.ConcentrationUm, w.SampleId))
            .ToList();

        await _mediator.SendAsync(new DesignPlateCommand(experimentId, wells), ct);

        return Ok(new ApiResult(true, "Plate designed."));
    }

    /// <summary>Imports the plate reader's raw absorbance from the canonical <c>well,absorbance</c> CSV.</summary>
    [HttpPost("{experimentId:guid}/import-reading")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ImportReading(
        Guid experimentId,
        [FromBody] ImportReadingRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(new ImportPlateReadingCommand(experimentId, body.CsvContent), ct);
        return Ok(new ApiResult(true, "Plate reading imported."));
    }

    /// <summary>
    /// Marks a plate well as an excluded outlier before the calculation runs (SISLAB-06). The exclusion is
    /// recorded with a reason and honoured by the strategies. Rejected once the snapshot is frozen (409).
    /// </summary>
    [HttpPost("{experimentId:guid}/wells/{coordinate}/exclude")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExcludeWell(
        Guid experimentId,
        string coordinate,
        [FromBody] ExcludeWellRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(new ExcludeWellCommand(experimentId, coordinate, body.Reason), ct);
        return Ok(new ApiResult(true, "Well excluded."));
    }

    /// <summary>Brings a previously excluded well back into the calculation (SISLAB-06). Rejected after freezing (409).</summary>
    [HttpPost("{experimentId:guid}/wells/{coordinate}/include")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IncludeWell(Guid experimentId, string coordinate, CancellationToken ct)
    {
        await _mediator.SendAsync(new IncludeWellCommand(experimentId, coordinate), ct);
        return Ok(new ApiResult(true, "Well re-included."));
    }

    /// <summary>Runs the versioned calculation (viability or nitric oxide) and freezes the result snapshot.</summary>
    [HttpPost("{experimentId:guid}/calculate")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Calculate(Guid experimentId, CancellationToken ct)
    {
        await _mediator.SendAsync(new CalculateExperimentCommand(experimentId), ct);
        return Ok(new ApiResult(true, "Calculation applied."));
    }

    /// <summary>
    /// Sets (or replaces) the experiment's lead responsible (card [E11]) — full edit authority over the
    /// experiment. The user must be an active member of the company. Coordination action, permission-gated.
    /// </summary>
    [HttpPut("{experimentId:guid}/responsible")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignResponsible(
        Guid experimentId,
        [FromBody] AssignResponsibleRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new AssignExperimentResponsibleCommand(experimentId, body.ResponsibleUserId), ct);

        return Ok(new ApiResult(true, "Experiment responsible assigned."));
    }

    /// <summary>
    /// Adds a responsible to a specific step (card [E11]) — step-scoped edit authority. The user must be an
    /// active member of the company. Coordination action, permission-gated.
    /// </summary>
    [HttpPost("{experimentId:guid}/steps/{stepId:guid}/responsibles")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignStepResponsible(
        Guid experimentId,
        Guid stepId,
        [FromBody] AssignResponsibleRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new AssignStepResponsibleCommand(experimentId, stepId, body.ResponsibleUserId), ct);

        return Ok(new ApiResult(true, "Step responsible assigned."));
    }

    /// <summary>Removes a responsible from a specific step (card [E11]). Idempotent. Permission-gated.</summary>
    [HttpDelete("{experimentId:guid}/steps/{stepId:guid}/responsibles/{responsibleUserId:guid}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveStepResponsible(
        Guid experimentId,
        Guid stepId,
        Guid responsibleUserId,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new RemoveStepResponsibleCommand(experimentId, stepId, responsibleUserId), ct);

        return Ok(new ApiResult(true, "Step responsible removed."));
    }

    /// <summary>
    /// Exports the calculated experiment as a GraphPad Prism-compatible CSV (card [E11] #79). Any member may
    /// export — it is a read of the frozen snapshot, so it is page-level <c>[Authorize]</c>, not permission-gated.
    /// </summary>
    [HttpGet("{experimentId:guid}/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Export(Guid experimentId, CancellationToken ct)
    {
        ExperimentExportDto export = await _mediator.SendAsync(new ExportExperimentQuery(experimentId), ct);

        return File(
            System.Text.Encoding.UTF8.GetBytes(export.CsvContent),
            export.ContentType,
            export.FileName);
    }

    /// <summary>
    /// Exports a calculated in vivo behavioural experiment as a Prism-compatible CSV laid out group × timepoint
    /// (card [E11] #31). Like the in vitro export, it is a read of the frozen snapshot, so it is page-level
    /// <c>[Authorize]</c>, not permission-gated.
    /// </summary>
    [HttpGet("{experimentId:guid}/export-behavioral")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExportBehavioral(Guid experimentId, CancellationToken ct)
    {
        ExperimentExportDto export =
            await _mediator.SendAsync(new ExportBehavioralExperimentQuery(experimentId), ct);

        return File(
            System.Text.Encoding.UTF8.GetBytes(export.CsvContent),
            export.ContentType,
            export.FileName);
    }
}

/// <summary>Request body to create a plate experiment; the company comes from the session, never the payload.</summary>
public sealed record CreateExperimentRequest(
    SISLAB.Modules.Experiments.Domain.Experiments.ExperimentType Type,
    string Title,
    string? Description,
    Guid? CompoundPartnerId);

/// <summary>Request body to assign a responsible (experiment lead or step): the target user's Lumen id.</summary>
public sealed record AssignResponsibleRequest(Guid ResponsibleUserId);

/// <summary>Request body to design the plate: the full set of wells.</summary>
public sealed record DesignPlateRequest(IReadOnlyList<DesignPlateWellRequest> Wells);

/// <summary>One well in a plate-design request.</summary>
public sealed record DesignPlateWellRequest(
    char Row,
    int Column,
    SISLAB.Modules.Experiments.Domain.Plates.WellRole Role,
    decimal? ConcentrationUm,
    string? SampleId);

/// <summary>Request body to import a plate reading — the canonical <c>well,absorbance</c> CSV as text.</summary>
public sealed record ImportReadingRequest(string CsvContent);

/// <summary>Request body to exclude a well as an outlier (SISLAB-06): the operator's reason.</summary>
public sealed record ExcludeWellRequest(string Reason);

/// <summary>
/// Request body to create an in vivo behavioural experiment; the company comes from the session, never the payload.
/// </summary>
public sealed record CreateBehavioralExperimentRequest(
    SISLAB.Modules.Experiments.Domain.Experiments.ExperimentType Type,
    string Title,
    string? Description,
    Guid ProjectId,
    Guid BatchId,
    IReadOnlyList<string> TimepointLabels);

/// <summary>Request body to record one behavioural timepoint: its label and one reading per animal.</summary>
public sealed record RecordTimepointRequest(
    string TimepointLabel,
    IReadOnlyList<TimepointReadingRequest> Readings);

/// <summary>One animal's raw reading at the timepoint (the animal id by value + the raw value string).</summary>
public sealed record TimepointReadingRequest(Guid AnimalId, string RawValue);
