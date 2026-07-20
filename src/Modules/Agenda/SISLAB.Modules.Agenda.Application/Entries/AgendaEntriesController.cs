using Lumen.Authorization.AspNetCore;
using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Agenda.Application.Entries.Commands;
using SISLAB.Modules.Agenda.Application.Entries.Queries;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Entries;

/// <summary>
/// HTTP surface for the improved calendar entries (card [E10.3] #3). Write actions are gated by Lumen
/// <c>[RequirePermission]</c> — the implicit code is <c>AgendaEntries.{Action}</c>, seeded by the E10 permission
/// migration. Multitenancy is defense-in-depth: the command carries the active company from
/// <see cref="ITenantContext"/> (never the body), and the responsible person is the authenticated principal
/// resolved via <see cref="IUserIdAccessor"/>, not a client-supplied id.
/// </summary>
/// <remarks>
/// Recurrence is expressed as an RFC 5545 <c>RRULE</c> string on the request body (e.g.
/// <c>FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=20260930T000000Z</c>); Swagger examples on the request records document the
/// shape. Editing a recurring entry passes an <c>editScope</c> (only-this / this-and-following / all).
/// </remarks>
[Route("api/agenda/entries")]
[Authorize]
public sealed class AgendaEntriesController : SislabControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdAccessor _userIdAccessor;

    public AgendaEntriesController(IMediator mediator, IUserIdAccessor userIdAccessor)
    {
        _mediator = mediator;
        _userIdAccessor = userIdAccessor;
    }

    /// <summary>
    /// Returns every occurrence in the date range for the calendar view (card [E10.4] #4): one-off entries plus
    /// each expanded instance of a recurring series. Filters are opt-in; <c>onlyMine</c> restricts to the
    /// caller's own entries.
    /// </summary>
    [HttpGet("/api/agenda/calendar")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<CalendarItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendar(
        [FromQuery] DateOnly start,
        [FromQuery] DateOnly end,
        [FromQuery] AgendaActivityType? activityType,
        [FromQuery] Guid? responsibleId,
        [FromQuery] Guid? experimentId,
        [FromQuery] bool onlyMine,
        CancellationToken ct)
    {
        var filters = new CalendarFilters(
            activityType, responsibleId, experimentId, onlyMine, CurrentUserIdOrNull());

        IReadOnlyList<CalendarItem> items = await _mediator.SendAsync(
            new GetCalendarQuery(start, end, filters), ct);

        return Ok(new ApiResult<IReadOnlyList<CalendarItem>>(true, "Calendar retrieved.", items));
    }

    /// <summary>Creates a calendar entry (one-off or the head of a recurring series).</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateAgendaEntryRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateAgendaEntryCommand(
                body.Title, body.Description, body.StartDateUtc, body.EndDateUtc, body.IsAllDay,
                body.ActivityType, body.ExperimentId, body.RecurrenceRule, ResolveResponsible()),
            ct);

        return Ok(new ApiResult<Guid>(true, "Agenda entry created.", id));
    }

    /// <summary>
    /// Updates an entry with Google-Calendar edit semantics. Returns the id of the entry to display after the
    /// edit (the original for an in-place edit; the new detached/split entry otherwise).
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgendaEntryRequest body, CancellationToken ct)
    {
        Guid resultId = await _mediator.SendAsync(
            new UpdateAgendaEntryCommand(
                id, body.EditScope, body.OccurrenceDate, body.Title, body.Description,
                body.StartDateUtc, body.EndDateUtc, body.IsAllDay, body.ActivityType,
                body.ExperimentId, body.RecurrenceRule),
            ct);

        return Ok(new ApiResult<Guid>(true, "Agenda entry updated.", resultId));
    }

    /// <summary>Cancels a single occurrence of a recurring entry (adds an RFC 5545 EXDATE).</summary>
    [HttpDelete("{id:guid}/occurrences/{date}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelOccurrence(Guid id, string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out DateOnly occurrenceDate))
            throw new BusinessException($"'{date}' is not a valid occurrence date (expected yyyy-MM-dd).");

        await _mediator.SendAsync(new CancelAgendaOccurrenceCommand(id, occurrenceDate), ct);
        return Ok(new ApiResult(true, "Occurrence cancelled."));
    }

    /// <summary>Deletes an entry outright — for a recurring entry this removes the whole series.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.SendAsync(new DeleteAgendaEntryCommand(id), ct);
        return Ok(new ApiResult(true, "Agenda entry deleted."));
    }

    private Guid ResolveResponsible()
    {
        if (!_userIdAccessor.TryGetUserId(User, out Guid userId) || userId == Guid.Empty)
            throw new ForbiddenException("The current user could not be resolved from the request principal.");

        return userId;
    }

    /// <summary>
    /// The authenticated user's id, or <see langword="null"/> when it cannot be resolved. Used only to back the
    /// opt-in <c>onlyMine</c> filter, so a read need not hard-fail when the principal is thin.
    /// </summary>
    private Guid? CurrentUserIdOrNull()
        => _userIdAccessor.TryGetUserId(User, out Guid userId) && userId != Guid.Empty ? userId : null;
}

/// <summary>Request body for creating a calendar entry (card [E10.3] #3).</summary>
/// <param name="RecurrenceRule">
/// Optional RFC 5545 RRULE, e.g. <c>FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=20260930T000000Z</c>. Null for a one-off.
/// </param>
public sealed record CreateAgendaEntryRequest(
    string Title,
    string? Description,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsAllDay,
    AgendaActivityType ActivityType,
    Guid? ExperimentId,
    string? RecurrenceRule);

/// <summary>
/// Request body for updating a calendar entry (card [E10.3] #3). <see cref="OccurrenceDate"/> is required for a
/// scoped edit of a recurring series (<see cref="EditScope.OnlyThis"/> / <see cref="EditScope.ThisAndFollowing"/>).
/// </summary>
/// <param name="RecurrenceRule">
/// Optional RFC 5545 RRULE, e.g. <c>FREQ=DAILY;COUNT=10</c>. Null clears recurrence (makes the entry a one-off).
/// </param>
public sealed record UpdateAgendaEntryRequest(
    EditScope EditScope,
    DateOnly? OccurrenceDate,
    string Title,
    string? Description,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsAllDay,
    AgendaActivityType ActivityType,
    Guid? ExperimentId,
    string? RecurrenceRule);
