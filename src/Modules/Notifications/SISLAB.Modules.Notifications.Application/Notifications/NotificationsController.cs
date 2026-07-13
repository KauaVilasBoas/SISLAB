using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Notifications.Application.NotificationsRead;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Notifications.Application.Notifications;

/// <summary>
/// HTTP boundary for the notification bell of the <b>active company</b> (card #64a): listing notifications
/// (all or unread), the unread badge count, and marking a notification as read. The controller only dispatches
/// CQRS queries/commands through <see cref="IMediator"/> and maps the result to the uniform
/// <see cref="ApiResult"/> envelope; it never touches repositories, the DbContext or Dapper, and never maps
/// errors — those bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every query/command runs against the active company resolved from the httpOnly cookie
/// (<c>ITenantContext</c> + read-side <c>WHERE company_id</c>), never from the request.
/// </remarks>
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the company's notifications, newest first; pass <c>unreadOnly=true</c> for just the unread ones.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PagedResult<NotificationListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken ct)
    {
        PagedResult<NotificationListItem> result = await _mediator.SendAsync(
            new ListNotificationsQuery
            {
                UnreadOnly = unreadOnly,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<NotificationListItem>>(true, "Notifications listed.", result));
    }

    /// <summary>Returns the unread notification count for the bell badge.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResult<UnreadNotificationsCount>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        UnreadNotificationsCount result =
            await _mediator.SendAsync(new CountUnreadNotificationsQuery(), ct);

        return Ok(new ApiResult<UnreadNotificationsCount>(true, "Unread count.", result));
    }

    /// <summary>Marks a notification as read, clearing it from the unread badge. Idempotent.</summary>
    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken ct)
    {
        await _mediator.SendAsync(new MarkNotificationAsReadCommand(notificationId), ct);

        return Ok(new ApiResult(true, "Notification marked as read."));
    }
}
