using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Agenda.Application.Bioterium.Commands;
using SISLAB.Modules.Agenda.Application.Bioterium.Queries;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Bioterium;

[Route("api/bioterium")]
[Authorize]
public sealed class BioteriumController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public BioteriumController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<BioteriumAssignmentItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        IReadOnlyList<BioteriumAssignmentItem> items = await _mediator.SendAsync(
            new GetBioteriumScheduleQuery(from, to), ct);
        return Ok(new ApiResult<IReadOnlyList<BioteriumAssignmentItem>>(true, "Schedule retrieved.", items));
    }

    [HttpPost("generate")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate([FromBody] GenerateWeekRequest body, CancellationToken ct)
    {
        await _mediator.SendAsync(new GenerateBioteriumWeekCommand(body.MondayOfWeek, body.ResponsibleNames), ct);
        return Ok(new ApiResult(true, "Biotério week generated."));
    }

    [HttpPost("{assignmentId:guid}/swap")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Swap(Guid assignmentId, [FromBody] SwapRequest body, CancellationToken ct)
    {
        await _mediator.SendAsync(new SwapBioteriumCommand(assignmentId, body.NewResponsibleName, body.Reason), ct);
        return Ok(new ApiResult(true, "Biotério assignment swapped."));
    }

    [HttpPost("{assignmentId:guid}/done")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkDone(Guid assignmentId, [FromBody] MarkDoneRequest body, CancellationToken ct)
    {
        await _mediator.SendAsync(new MarkBioteriumDoneCommand(assignmentId, body.Notes), ct);
        return Ok(new ApiResult(true, "Biotério assignment marked done."));
    }
}

public sealed record GenerateWeekRequest(DateOnly MondayOfWeek, IReadOnlyList<string> ResponsibleNames);
public sealed record SwapRequest(string NewResponsibleName, string? Reason);
public sealed record MarkDoneRequest(string? Notes);
