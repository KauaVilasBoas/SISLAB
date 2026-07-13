using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.ExpiryPolicies;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's expiry policy (card [E12] #76): reading and setting the "expiring
/// soon" warning window. The controller only dispatches CQRS requests through <see cref="IMediator"/> and
/// maps the result to the uniform <see cref="ApiResult"/> envelope; it never touches repositories, the
/// DbContext or Dapper, and never maps errors — those bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from
/// the request body. RBAC permissions are layered on in card #77.
/// </remarks>
[Route("api/configuration/expiry-policy")]
[Authorize]
public sealed class ExpiryPolicyController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public ExpiryPolicyController(IMediator mediator) => _mediator = mediator;

    /// <summary>Returns the active company's expiry warning window in days (the default when none is set yet).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWarningWindow(CancellationToken ct)
    {
        int days = await _mediator.SendAsync(new GetExpiryWarningWindowQuery(), ct);

        return Ok(new ApiResult<int>(true, "Expiry warning window retrieved.", days));
    }

    /// <summary>Sets the active company's expiry warning window (creating the policy on first configuration).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetWarningWindow(
        [FromBody] SetExpiryWarningWindowRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(new SetExpiryWarningWindowCommand(body.WarningWindowDays), ct);

        return Ok(new ApiResult(true, "Expiry warning window updated."));
    }
}

/// <summary>Request body for setting the expiry warning window.</summary>
public sealed record SetExpiryWarningWindowRequest(int WarningWindowDays);
