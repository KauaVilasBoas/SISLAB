using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>
/// Admin endpoints for managing members of the <b>active company</b>.
///
/// <para>This is an <b>MVC controller</b> (not a Minimal API) for a precise architectural reason:
/// Lumen's permission discovery scanner (<c>PermissionDiscoveryScanner</c>) only iterates
/// <c>ControllerActionDescriptor</c>s — Minimal API endpoints are invisible to it. Decorating
/// actions with <see cref="RequirePermissionAttribute"/> causes Lumen to materialize the
/// permission codes and reconcile them into the <c>Administrator</c> profile on startup.</para>
///
/// <para><b>Permission code convention (enforced by Lumen 1.1.0):</b> the persisted permission is
/// always <c>&lt;Controller&gt;.&lt;Action&gt;</c>. <c>Permission.Create</c> recomputes the code
/// from the controller name (without the <c>Controller</c> suffix) and the method name, IGNORING
/// any explicit string passed to the attribute. Therefore actions are decorated with
/// <c>[RequirePermission]</c> WITHOUT an explicit code: discovery (which writes
/// <c>Controller.Action</c>) and enforcement (which, with a null attribute code, derives
/// <c>Controller.Action</c> from the descriptor) agree. Method names (<c>ListMembers</c>,
/// <c>CheckRemovalEligibility</c>) are the single source of truth for codes; see
/// <c>SISLAB.Modules.Identity.Contracts.Authorization.IdentityPermissions</c>.</para>
///
/// <para><b>Tenant-scoped:</b> all actions operate on the active company (from the httpOnly cookie),
/// read from <see cref="SislabControllerBase.GetCompanyId"/> — never from the request body. The
/// controller only dispatches CQRS queries via <see cref="IMediator"/> and maps the result to HTTP;
/// it never touches repositories or the DbContext directly.</para>
/// </summary>
[Route("api/admin/companies/active/members")]
[Authorize]
public sealed class CompanyMembersController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public CompanyMembersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists members of the active company. Requires <c>CompanyMembers.ListMembers</c>
    /// permission scoped to the active company.
    /// </summary>
    [HttpGet(Name = "ListMembers")]
    [ActionName("ListMembers")]
    [RequirePermission]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(CancellationToken ct)
    {
        if (!HasActiveTenant())
            return NoActiveCompany();

        ListCompanyMembersResult result =
            await _mediator.SendAsync(new ListCompanyMembersQuery(GetCompanyId()), ct);

        return result.CompanyExists ? Ok(result.Members) : NoActiveCompany();
    }

    /// <summary>
    /// Dry-runs removal eligibility for a member of the active company.
    /// Requires <c>CompanyMembers.CheckRemovalEligibility</c> permission scoped to the active company.
    ///
    /// Exists as a second decorated action (write permission) to prove that discovery
    /// materializes and reconciles more than one permission code. The actual member removal
    /// (CQRS write-side command) is a later card; here the focus is exclusively permission
    /// enforcement.
    /// </summary>
    [HttpGet("{userId:guid}/removal-eligibility", Name = "CheckRemovalEligibility")]
    [ActionName("CheckRemovalEligibility")]
    [RequirePermission]
    [ProducesResponseType(typeof(MemberRemovalEligibilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckRemovalEligibility(Guid userId, CancellationToken ct)
    {
        if (!HasActiveTenant())
            return NoActiveCompany();

        CheckMemberRemovalEligibilityResult result =
            await _mediator.SendAsync(
                new CheckMemberRemovalEligibilityQuery(GetCompanyId(), userId), ct);

        return result.CompanyExists ? Ok(result.Eligibility) : NoActiveCompany();
    }
}
