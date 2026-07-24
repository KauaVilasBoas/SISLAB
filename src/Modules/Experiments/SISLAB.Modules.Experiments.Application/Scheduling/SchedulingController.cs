using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Experiments.Application.Scheduling.Commands;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Scheduling;

/// <summary>
/// HTTP boundary for experiment scheduling (SISLAB-10). It exposes the single write action that derives an
/// experiment's calendar from its bound experimental model / induction protocol and materialises it in the Agenda
/// module, rotating a configurable roster of responsibles. The controller only dispatches the CQRS command through
/// <see cref="IMediator"/> and maps the result to the uniform <see cref="ApiResult{T}"/> envelope; the "what happens
/// on which day" derivation, the Agenda hand-off and the roster validation all live in the command handler.
/// </summary>
/// <remarks>
/// Tenant-scoped: the active company is resolved from the httpOnly cookie into <c>ITenantContext</c>; the handler
/// validates every roster member against the active company through the Identity Contracts port and reads the model
/// through the Configuration Contracts port, never trusting the request body for tenancy. The action is gated by
/// Lumen's <c>[RequirePermission]</c> (code <c>Scheduling.Generate</c>).
/// </remarks>
[Route("api/experiments/{experimentId:guid}/schedule")]
[Authorize]
public sealed class SchedulingController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public SchedulingController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Generates the experiment's schedule from its experimental model (SISLAB-10) and creates the Agenda entries,
    /// rotating the roster across the days. Returns the created Agenda entry ids in chronological order.
    /// </summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<GenerateExperimentScheduleResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Generate(
        Guid experimentId,
        [FromBody] GenerateScheduleRequest body,
        CancellationToken ct)
    {
        GenerateExperimentScheduleResult result = await _mediator.SendAsync(
            new GenerateExperimentScheduleCommand(
                experimentId,
                body.ExperimentalModelId,
                body.StartDate,
                body.TreatmentDayOffsets ?? [],
                body.TimepointDayOffsets ?? [],
                body.Responsibles ?? [],
                body.DaysPerShift,
                body.ReminderMinutesBefore),
            ct);

        return Ok(new ApiResult<GenerateExperimentScheduleResult>(true, "Experiment schedule generated.", result));
    }
}

/// <summary>
/// Request body to generate an experiment schedule (SISLAB-10). The experiment id comes from the route and the
/// company from the session; everything else parameterizes the derivation from the model — nothing lab-specific is
/// fixed by the endpoint.
/// </summary>
/// <param name="ExperimentalModelId">The model whose induction protocol drives the cadence (validated via Configuration).</param>
/// <param name="StartDate">Day 0 of the schedule (the first induction day).</param>
/// <param name="TreatmentDayOffsets">Day offsets (from the start) of treatment days, derived from the model.</param>
/// <param name="TimepointDayOffsets">One day offset per model timepoint, in the model's timepoint order.</param>
/// <param name="Responsibles">The ordered rotation list of responsible member ids.</param>
/// <param name="DaysPerShift">How many consecutive days one responsible covers before rotation (≥ 1).</param>
/// <param name="ReminderMinutesBefore">Optional véspera-reminder lead time in minutes; <see langword="null"/> for none.</param>
public sealed record GenerateScheduleRequest(
    Guid ExperimentalModelId,
    DateOnly StartDate,
    IReadOnlyList<int>? TreatmentDayOffsets,
    IReadOnlyList<int>? TimepointDayOffsets,
    IReadOnlyList<Guid>? Responsibles,
    int DaysPerShift,
    int? ReminderMinutesBefore);
