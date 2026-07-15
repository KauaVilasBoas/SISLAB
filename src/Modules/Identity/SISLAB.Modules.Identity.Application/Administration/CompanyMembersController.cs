using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>
/// Admin endpoints for managing members of the <b>active company</b>.
///
/// <para>This is an <b>MVC controller</b> (not a Minimal API) because Lumen's authorization filter
/// enforces <see cref="RequirePermissionAttribute"/> on <c>ControllerActionDescriptor</c>s — Minimal
/// API endpoints are invisible to it.</para>
///
/// <para><b>Permission code convention (enforced by Lumen):</b> the enforced permission is always
/// <c>&lt;Controller&gt;.&lt;Action&gt;</c>. With a null attribute code Lumen derives
/// <c>Controller.Action</c> from the descriptor (controller name without the <c>Controller</c> suffix,
/// plus the action name), so actions are decorated with <c>[RequirePermission]</c> WITHOUT an explicit
/// code. Method names (<c>ListMembers</c>, <c>CheckRemovalEligibility</c>) are the single source of
/// truth for the codes; the matching permission rows are seeded by the <c>SISLAB.Migrations</c> project.</para>
///
/// <para><b>Tenant-scoped:</b> all actions operate on the active company (from the httpOnly cookie),
/// read from <see cref="SislabControllerBase.GetCompanyId"/> — never from the request body. The
/// controller only dispatches CQRS queries via <see cref="IMediator"/> and maps the successful
/// result to HTTP; it never touches repositories or the DbContext, and never maps errors — those
/// bubble up to the exception-handling middleware as the uniform <see cref="ApiResult"/> envelope.</para>
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
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<CompanyMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(CancellationToken ct)
    {
        ListCompanyMembersQueryResult result =
            await _mediator.SendAsync(new ListCompanyMembersQuery(GetCompanyId()), ct);

        return Ok(new ApiResult<IReadOnlyList<CompanyMemberDto>>(
            true, "Members retrieved.", result.Members));
    }

    /// <summary>
    /// Lists members of the active company enriched with their Lumen identity (username/e-mail) and assigned
    /// profiles — the data the "Members" tab renders (card [E7] #105). Requires the
    /// <c>CompanyMembers.ListEnrichedMembers</c> permission scoped to the active company.
    /// </summary>
    [HttpGet("enriched", Name = "ListEnrichedMembers")]
    [ActionName("ListEnrichedMembers")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<EnrichedMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListEnrichedMembers(CancellationToken ct)
    {
        ListEnrichedCompanyMembersQueryResult result =
            await _mediator.SendAsync(new ListEnrichedCompanyMembersQuery(GetCompanyId()), ct);

        return Ok(new ApiResult<IReadOnlyList<EnrichedMemberDto>>(
            true, "Enriched members retrieved.", result.Members));
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
    [ProducesResponseType(typeof(ApiResult<MemberRemovalEligibilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckRemovalEligibility(Guid userId, CancellationToken ct)
    {
        CheckMemberRemovalEligibilityQueryResult result =
            await _mediator.SendAsync(
                new CheckMemberRemovalEligibilityQuery(GetCompanyId(), userId), ct);

        return Ok(new ApiResult<MemberRemovalEligibilityDto>(
            true, "Member removal eligibility evaluated.", result.Eligibility));
    }
}
