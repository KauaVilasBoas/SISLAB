using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Administration;

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
/// <c>Controller.Action</c> from the descriptor) agree. Passing an explicit code breaks
/// enforcement — the handler would compare the attribute code against the stored
/// <c>Controller.Action</c> and always deny (403). Method names (<c>ListMembers</c>,
/// <c>CheckRemovalEligibility</c>) are the single source of truth for codes; see
/// <c>SISLAB.Modules.Identity.Contracts.Authorization.IdentityPermissions</c>.</para>
///
/// <para><b>Tenant-scoped:</b> all actions operate on <see cref="ITenantContext.CompanyId"/>
/// (the active company from the httpOnly cookie). Lumen's <c>PermissionAuthorizationHandler</c>
/// resolves the user's permission <i>within the scope</i> of that company (via
/// <c>ITenantScopeAccessor</c> → <c>SislabTenantScopeAccessor</c>): holding the Administrator
/// profile in LAFTE grants access when LAFTE is active and denies (403) when another company
/// is active.</para>
/// </summary>
[ApiController]
[Route("api/admin/companies/active/members")]
[Authorize]
public sealed class CompanyMembersController : ControllerBase
{
    private readonly ITenantContext _tenantContext;
    private readonly ICompanyRepository _companyRepository;

    public CompanyMembersController(
        ITenantContext tenantContext,
        ICompanyRepository companyRepository)
    {
        _tenantContext = tenantContext;
        _companyRepository = companyRepository;
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
        Guid companyId = _tenantContext.CompanyId;
        if (companyId == Guid.Empty)
            return NotFoundNoActiveCompany();

        Company? company = await _companyRepository.FindByIdAsync(companyId, ct);
        if (company is null)
            return NotFoundNoActiveCompany();

        var members = company.Memberships
            .Select(m => new CompanyMemberDto(m.Id, m.LumenUserId))
            .ToList();

        return Ok(members);
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckRemovalEligibility(Guid userId, CancellationToken ct)
    {
        Guid companyId = _tenantContext.CompanyId;
        if (companyId == Guid.Empty)
            return NotFoundNoActiveCompany();

        Company? company = await _companyRepository.FindByIdAsync(companyId, ct);
        if (company is null)
            return NotFoundNoActiveCompany();

        bool isMember = company.Memberships.Any(m => m.LumenUserId == userId);
        return Ok(new { userId, isMember, canRemove = isMember });
    }

    private IActionResult NotFoundNoActiveCompany()
        => Problem(
            title: "No active company",
            detail: "Select an active company via POST /api/companies/{companyId}/activate.",
            statusCode: StatusCodes.Status404NotFound);
}
