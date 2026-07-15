using Lumen.Authorization.AspNetCore;
using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Invitations;

/// <summary>
/// Admin HTTP boundary for inviting members to the <b>active company</b> by e-mail (card [E12] #75c).
///
/// <para><b>Tenant-scoped &amp; permission-gated:</b> the target company is the active one, read from
/// <see cref="SislabControllerBase.GetCompanyId"/> (the httpOnly cookie), never from the body — so a coordinator
/// can only invite into their own company. The single write action carries
/// <c>[RequirePermission(CompanyMembers.InviteMember)]</c>, so a non-coordinator without that code in the active
/// company gets 403. The inviter's id comes from the authenticated principal via <see cref="IUserIdAccessor"/>,
/// never from the request.</para>
///
/// <para>The controller only dispatches the CQRS command through the SISLAB <see cref="IMediator"/> and maps the
/// result to the uniform <see cref="ApiResult"/> envelope; it never touches Lumen, repositories or the DbContext
/// directly.</para>
/// </summary>
[Route("api/admin/companies/active/members")]
[Authorize]
public sealed class CompanyMemberInvitationsController : SislabControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdAccessor _userIdAccessor;

    public CompanyMemberInvitationsController(IMediator mediator, IUserIdAccessor userIdAccessor)
    {
        _mediator = mediator;
        _userIdAccessor = userIdAccessor;
    }

    /// <summary>
    /// Invites a person to the active company by e-mail, granting the chosen profile on accept. Requires the
    /// <c>CompanyMembers.InviteMember</c> permission scoped to the active company. A resend to an e-mail with a
    /// pending invitation is idempotent (re-sends the same invitation). Inviting an existing member returns 409.
    /// </summary>
    [HttpPost("invite", Name = "InviteMember")]
    [ActionName("InviteMember")]
    [RequirePermission("CompanyMembers.InviteMember")]
    [ProducesResponseType(typeof(ApiResult<InviteMemberResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> InviteMember(
        [FromBody] InviteMemberRequest body,
        CancellationToken ct)
    {
        if (!_userIdAccessor.TryGetUserId(User, out Guid invitedByUserId) || invitedByUserId == Guid.Empty)
            return Unauthorized();

        InviteMemberResult result = await _mediator.SendAsync(
            new InviteMemberCommand(GetCompanyId(), body.Email, body.ProfileId, invitedByUserId), ct);

        string message = result.Resent ? "Invitation re-sent." : "Invitation sent.";
        return Ok(new ApiResult<InviteMemberResult>(true, message, result));
    }
}

/// <summary>Request body for inviting a member: the invitee e-mail and the Lumen profile to grant on accept.</summary>
/// <param name="Email">Invitee e-mail.</param>
/// <param name="ProfileId">Lumen profile granted (company-scoped) when the invitation is accepted.</param>
public sealed record InviteMemberRequest(string Email, Guid ProfileId);
