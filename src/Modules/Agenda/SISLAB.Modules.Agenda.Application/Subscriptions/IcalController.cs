using Lumen.Authorization.AspNetCore;
using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Agenda.Application.Subscriptions.Commands;
using SISLAB.Modules.Agenda.Application.Subscriptions.Queries;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Subscriptions;

/// <summary>
/// iCal feed surface (card [E10.10]). Two endpoints with opposite security models: <see cref="Subscribe"/> is
/// authenticated and mints/rotates the calling user's token; <see cref="GetFeed"/> is public and session-less —
/// an external calendar client polls it with the token in the query string. The token is the capability, so the
/// feed carries its own tenant and never touches the auth cookie or <c>ITenantContext</c>.
/// </summary>
[Route("api/agenda")]
public sealed class IcalController : SislabControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdAccessor _userIdAccessor;

    public IcalController(IMediator mediator, IUserIdAccessor userIdAccessor)
    {
        _mediator = mediator;
        _userIdAccessor = userIdAccessor;
    }

    /// <summary>
    /// Generates or rotates the calling user's iCal feed token for the active company. Returns the token to embed
    /// in the <c>calendar.ics?token=...</c> URL. Calling it again revokes the previous URL by rotating the token.
    /// </summary>
    [HttpPost("ical/subscribe")]
    [Authorize]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<IcalSubscriptionResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Subscribe(CancellationToken ct)
    {
        if (!_userIdAccessor.TryGetUserId(User, out Guid userId) || userId == Guid.Empty)
            throw new ForbiddenException("The current user could not be resolved from the request principal.");

        IcalSubscriptionResult result = await _mediator.SendAsync(new SubscribeToIcalCommand(userId), ct);
        return Ok(new ApiResult<IcalSubscriptionResult>(true, "iCal subscription ready.", result));
    }

    /// <summary>
    /// Public iCal feed for a subscription token (card [E10.10]). Returns <c>text/calendar</c> so a calendar
    /// client can subscribe to it directly. An unknown token yields 404 without revealing whether it ever existed.
    /// </summary>
    [HttpGet("calendar.ics")]
    [AllowAnonymous]
    [Produces("text/calendar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFeed([FromQuery] Guid token, CancellationToken ct)
    {
        IcalFeedResult feed = await _mediator.SendAsync(new GetIcalFeedQuery(token), ct);

        if (!feed.Found)
            return NotFound();

        return File(System.Text.Encoding.UTF8.GetBytes(feed.Content), "text/calendar", "calendar.ics");
    }
}
