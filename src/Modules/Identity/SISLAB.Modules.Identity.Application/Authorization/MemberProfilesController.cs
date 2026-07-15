using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// HTTP boundary for assigning/removing authorization profiles to members of the <b>active company</b>
/// (card [E12] #104). Assignment is company-scoped (<c>UserProfile.ScopeId = companyId</c>): a member's
/// profiles here grant permissions inside this tenant only.
///
/// <para><b>Tenant-scoped:</b> the active company comes from <see cref="SislabControllerBase.GetCompanyId"/>
/// (the httpOnly cookie), never from the path or body — the path only carries the target member's user id.
/// The command handlers enforce that the target is a member of the active company, so an operator cannot
/// assign/remove profiles for users of another tenant (isolation). Both actions carry <c>[RequirePermission]</c>
/// (prefix <c>MemberProfiles</c>), gating the capability to whoever holds the code in the active company.</para>
///
/// <para>The controller only dispatches CQRS commands through the SISLAB <see cref="IMediator"/> and maps the
/// result to the uniform <see cref="ApiResult"/> envelope; it never touches Lumen, repositories or the
/// DbContext directly.</para>
/// </summary>
[Route("api/admin/companies/active/members/{userId:guid}/profiles")]
[Authorize]
public sealed class MemberProfilesController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public MemberProfilesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Assigns a profile to a member of the active company, scoped to it (card #104). Requires the
    /// <c>MemberProfiles.AssignProfile</c> permission.
    /// </summary>
    [HttpPost(Name = "AssignProfile")]
    [ActionName("AssignProfile")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignProfile(
        Guid userId,
        [FromBody] AssignProfileRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new AssignProfileToMemberCommand(GetCompanyId(), userId, body.ProfileId), ct);

        return Ok(new ApiResult(true, "Profile assigned to member."));
    }

    /// <summary>
    /// Removes a company-scoped profile assignment from a member of the active company (card #104). Requires the
    /// <c>MemberProfiles.RemoveProfile</c> permission.
    /// </summary>
    [HttpDelete("{profileId:guid}", Name = "RemoveProfile")]
    [ActionName("RemoveProfile")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProfile(Guid userId, Guid profileId, CancellationToken ct)
    {
        await _mediator.SendAsync(
            new RemoveProfileFromMemberCommand(GetCompanyId(), userId, profileId), ct);

        return Ok(new ApiResult(true, "Profile removed from member."));
    }
}

/// <summary>Request body for assigning a profile to a member: the profile id to grant.</summary>
public sealed record AssignProfileRequest(Guid ProfileId);
