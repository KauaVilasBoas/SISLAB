using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Identity.Contracts.Invitations;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Invitations;

/// <summary>
/// Public HTTP boundary for consuming a member invitation (card [E12] #75c): previewing it and accepting it.
///
/// <para><b>Anonymous by design:</b> the invitee has no account and no active company when they follow the
/// e-mail link, so the token itself is the credential. Both actions are <c>[AllowAnonymous]</c> — the same
/// carve-out as public signup, which the "writes must be permission-gated" architecture rule explicitly exempts
/// for anonymous actions. Because they run before any session cookie exists, they are also exempt from CSRF at
/// the Host, mirroring the public Lumen auth endpoints.</para>
///
/// <para>The controller only dispatches CQRS requests through the SISLAB <see cref="IMediator"/> and maps the
/// result to the uniform <see cref="ApiResult"/> envelope; it never touches Lumen, repositories or the DbContext
/// directly.</para>
/// </summary>
[Route("api/companies/invitations")]
public sealed class InvitationsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public InvitationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Previews an invitation by its token so the SPA can show the company, e-mail and profile before the
    /// invitee accepts — and whether the accept form must collect a username/password (new account) or just
    /// link an existing one. Anonymous. An unknown/invalid token returns 404.
    /// </summary>
    [HttpGet("{token}", Name = "PreviewInvitation")]
    [ActionName("PreviewInvitation")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResult<InvitationPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewInvitation(string token, CancellationToken ct)
    {
        InvitationPreviewDto preview = await _mediator.SendAsync(new PreviewInvitationQuery(token), ct);
        return Ok(new ApiResult<InvitationPreviewDto>(true, "Invitation preview retrieved.", preview));
    }

    /// <summary>
    /// Accepts an invitation by its token: the invitee joins the invited company with the invited profile. If
    /// the e-mail already has an account it is linked (no password needed); otherwise a new account is created
    /// from the supplied username/password. Anonymous. A double accept is idempotent; an expired invitation
    /// returns 422; an invalid token returns 404.
    /// </summary>
    [HttpPost("accept", Name = "AcceptInvitation")]
    [ActionName("AcceptInvitation")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResult<AcceptInvitationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptInvitationRequest body,
        CancellationToken ct)
    {
        AcceptInvitationResult result = await _mediator.SendAsync(
            new AcceptInvitationCommand(body.Token, body.Username, body.Password), ct);

        return Ok(new ApiResult<AcceptInvitationResult>(true, "Invitation accepted.", result));
    }
}

/// <summary>
/// Request body for accepting an invitation. <see cref="Username"/>/<see cref="Password"/> are required only
/// when the invitee has no account yet (new account); they are ignored when an existing account is linked.
/// </summary>
/// <param name="Token">The raw accept token from the invitation e-mail link.</param>
/// <param name="Username">Display/user name for a new account; ignored when linking an existing account.</param>
/// <param name="Password">Password for a new account; ignored when linking an existing account.</param>
public sealed record AcceptInvitationRequest(string Token, string? Username, string? Password);
