using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Experiments.Application.Biobank.Commands;
using SISLAB.Modules.Experiments.Application.Biobank.Queries;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Biobank;

/// <summary>
/// HTTP boundary for the biobank (card [E11] #89, decision F4 — its own <see cref="Sample"/> aggregate). It groups
/// the write side (collect sample → run analysis → record result) and the read side (list / detail with the
/// derived balance). The controller only dispatches CQRS requests through <see cref="IMediator"/> and maps the
/// result to the uniform <see cref="ApiResult"/>/<see cref="ApiResult{T}"/> envelope; it never touches
/// repositories, the DbContext or Dapper, and never maps errors — those bubble up to the exception middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie (EF Core global
/// query filter + <c>ITenantContext</c> on the write side; the read side keeps the mandatory
/// <c>WHERE company_id = @CompanyId</c>), never from the request body. Each state-changing action is gated by
/// Lumen's <c>[RequirePermission]</c>; the reads are page-level <c>[Authorize]</c>.
/// </remarks>
[Route("api/samples")]
[Authorize]
public sealed class SamplesController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public SamplesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's biobank samples, paginated, optionally filtered by project/type.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PagedResult<SampleListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        PagedResult<SampleListItem> result = await _mediator.SendAsync(
            new ListSamplesQuery { Page = page, PageSize = pageSize, ProjectId = projectId, Type = type },
            ct);

        return Ok(new ApiResult<PagedResult<SampleListItem>>(true, "Samples retrieved.", result));
    }

    /// <summary>Returns a single sample's detail — header, derived balance and the analyses run against it.</summary>
    [HttpGet("{sampleId:guid}")]
    [ProducesResponseType(typeof(ApiResult<SampleDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid sampleId, CancellationToken ct)
    {
        SampleDetail detail = await _mediator.SendAsync(new GetSampleQuery(sampleId), ct);
        return Ok(new ApiResult<SampleDetail>(true, "Sample retrieved.", detail));
    }

    /// <summary>Collects a sample from a study animal during an experiment's collection step. Returns the new id.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Collect([FromBody] CollectSampleRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CollectSampleCommand(
                body.SourceExperimentId,
                body.AnimalId,
                body.Code,
                body.Type,
                body.Quantity,
                body.Unit,
                body.ConservationTempMinCelsius,
                body.ConservationTempMaxCelsius,
                body.StorageLabel,
                body.Notes),
            ct);

        return Ok(new ApiResult<Guid>(true, "Sample collected.", id));
    }

    /// <summary>Runs an analysis against a sample, consuming an aliquot of its balance. Returns the new analysis id.</summary>
    [HttpPost("{sampleId:guid}/analyses")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Analyse(
        Guid sampleId,
        [FromBody] AnalyseSampleRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new AnalyseSampleCommand(sampleId, body.Name, body.ConsumedQuantity, body.Unit), ct);

        return Ok(new ApiResult<Guid>(true, "Analysis started.", id));
    }

    /// <summary>Records the result of a pending analysis and signs it off as completed.</summary>
    [HttpPost("{sampleId:guid}/analyses/{analysisId:guid}/result")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordResult(
        Guid sampleId,
        Guid analysisId,
        [FromBody] RecordAnalysisResultRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(new RecordAnalysisResultCommand(sampleId, analysisId, body.Result), ct);
        return Ok(new ApiResult(true, "Analysis result recorded."));
    }
}

/// <summary>Request body to collect a sample; the company comes from the session, never the payload.</summary>
public sealed record CollectSampleRequest(
    Guid SourceExperimentId,
    Guid AnimalId,
    string Code,
    SampleType Type,
    decimal Quantity,
    string Unit,
    decimal? ConservationTempMinCelsius,
    decimal? ConservationTempMaxCelsius,
    string? StorageLabel,
    string? Notes);

/// <summary>Request body to run an analysis against a sample (consumes an aliquot in the sample's unit).</summary>
public sealed record AnalyseSampleRequest(string Name, decimal ConsumedQuantity, string Unit);

/// <summary>Request body to record the result of a pending analysis.</summary>
public sealed record RecordAnalysisResultRequest(string Result);
