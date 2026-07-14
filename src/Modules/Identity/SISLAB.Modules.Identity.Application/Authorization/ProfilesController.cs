using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// HTTP boundary for authorization-profile management (cards [E12] #102/#103): listing the permission
/// catalogue grouped for the checkboxes and creating/editing profiles with their permissions. The controller
/// only dispatches CQRS requests through the SISLAB <see cref="IMediator"/> and maps the result to the uniform
/// <see cref="ApiResult"/> envelope; it never touches Lumen, MediatR, repositories or the DbContext directly —
/// those live behind <c>ILumenAuthorizationGateway</c> in the module's Infrastructure.
///
/// <para><b>Permission codes</b> follow the Lumen <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention: this
/// controller's prefix is <c>Profiles</c>, so its actions materialize <c>Profiles.ListAvailablePermissions</c>,
/// <c>Profiles.ListProfiles</c>, etc. Every action is decorated with <c>[RequirePermission]</c> (no explicit
/// code — Lumen recomputes it from controller + action), gating profile management to whoever holds the code
/// in the active company (e.g. coordination).</para>
///
/// <para>Profiles and the permission catalogue are <b>global</b> to the Lumen instance, not tenant-scoped: a
/// profile defines a reusable set of permissions. Scoping happens when a profile is <i>assigned</i> to a
/// member of a company (see <c>MemberProfilesController</c>, card #104). Permissions are read-only — there is
/// deliberately no action here that creates or edits a <c>Permission</c>.</para>
/// </summary>
[Route("api/admin/profiles")]
[Authorize]
public sealed class ProfilesController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public ProfilesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lists every permission grouped by <c>PermissionGroup</c> for the profile-management checkboxes
    /// (card #102). Pass <paramref name="profileId"/> when editing a profile to pre-select the permissions it
    /// already grants. Requires the <c>Profiles.ListAvailablePermissions</c> permission.
    /// </summary>
    [HttpGet("permissions", Name = "ListAvailablePermissions")]
    [ActionName("ListAvailablePermissions")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<PermissionGroupDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAvailablePermissions(
        [FromQuery] Guid? profileId,
        CancellationToken ct)
    {
        ListAvailablePermissionsResult result =
            await _mediator.SendAsync(new ListAvailablePermissionsQuery(profileId), ct);

        return Ok(new ApiResult<IReadOnlyList<PermissionGroupDto>>(
            true, "Permissions retrieved.", result.Groups));
    }

    /// <summary>
    /// Creates a profile from a name and description (card #103) and returns its new id. Permissions are set
    /// separately via <see cref="SetProfilePermissions"/>. Requires the <c>Profiles.CreateProfile</c> permission.
    /// </summary>
    [HttpPost(Name = "CreateProfile")]
    [ActionName("CreateProfile")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateProfileCommand(body.Name, body.Description ?? string.Empty), ct);

        return Ok(new ApiResult<Guid>(true, "Profile created.", id));
    }

    /// <summary>
    /// Renames/re-describes an existing profile (card #103). Requires the <c>Profiles.UpdateProfile</c> permission.
    /// </summary>
    [HttpPut("{profileId:guid}", Name = "UpdateProfile")]
    [ActionName("UpdateProfile")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile(
        Guid profileId,
        [FromBody] UpdateProfileRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new UpdateProfileCommand(profileId, body.Name, body.Description ?? string.Empty), ct);

        return Ok(new ApiResult(true, "Profile updated."));
    }

    /// <summary>
    /// Reconciles a profile's permissions to exactly the supplied set (card #103) — the "save" of the
    /// permission checkboxes, idempotent. The audit actor is taken from the authenticated principal, never from
    /// the body. Requires the <c>Profiles.SetProfilePermissions</c> permission.
    /// </summary>
    [HttpPut("{profileId:guid}/permissions", Name = "SetProfilePermissions")]
    [ActionName("SetProfilePermissions")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetProfilePermissions(
        Guid profileId,
        [FromBody] SetProfilePermissionsRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new SetProfilePermissionsCommand(
                profileId,
                body.PermissionIds ?? [],
                User.Identity?.Name),
            ct);

        return Ok(new ApiResult(true, "Profile permissions updated."));
    }
}

/// <summary>Request body for creating a profile. Description is optional (defaults to empty).</summary>
public sealed record CreateProfileRequest(string Name, string? Description);

/// <summary>Request body for updating a profile's identity. Description is optional (defaults to empty).</summary>
public sealed record UpdateProfileRequest(string Name, string? Description);

/// <summary>
/// Request body for setting a profile's permissions: the exact set of permission ids that should be granted
/// (the checked checkboxes). An empty/absent list clears all permissions.
/// </summary>
public sealed record SetProfilePermissionsRequest(IReadOnlyList<Guid>? PermissionIds);
