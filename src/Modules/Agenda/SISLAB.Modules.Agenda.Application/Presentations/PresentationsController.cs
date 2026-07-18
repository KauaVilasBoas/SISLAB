using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Agenda.Application.Presentations.Commands;
using SISLAB.Modules.Agenda.Application.Presentations.Queries;
using SISLAB.Modules.Agenda.Domain.Presentations;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Presentations;

[Route("api/presentations")]
[Authorize]
public sealed class PresentationsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public PresentationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<PresentationListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        IReadOnlyList<PresentationListItem> items = await _mediator.SendAsync(
            new GetPresentationScheduleQuery(from, to), ct);
        return Ok(new ApiResult<IReadOnlyList<PresentationListItem>>(true, "Presentations retrieved.", items));
    }

    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Schedule([FromBody] SchedulePresentationRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new SchedulePresentationCommand(body.Type, body.Title, body.Doi, body.PresenterName, body.ScheduledDate, body.Notes),
            ct);
        return Ok(new ApiResult<Guid>(true, "Presentation scheduled.", id));
    }

    [HttpPatch("{presentationId:guid}/reschedule")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reschedule(Guid presentationId, [FromBody] ReschedulePresentationRequest body, CancellationToken ct)
    {
        await _mediator.SendAsync(new ReschedulePresentationCommand(presentationId, body.NewDate, body.Notes), ct);
        return Ok(new ApiResult(true, "Presentation rescheduled."));
    }

    [HttpDelete("{presentationId:guid}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid presentationId, CancellationToken ct)
    {
        await _mediator.SendAsync(new CancelPresentationCommand(presentationId), ct);
        return Ok(new ApiResult(true, "Presentation cancelled."));
    }
}

public sealed record SchedulePresentationRequest(
    PresentationType Type,
    string Title,
    string? Doi,
    string PresenterName,
    DateOnly ScheduledDate,
    string? Notes);

public sealed record ReschedulePresentationRequest(DateOnly NewDate, string? Notes);
