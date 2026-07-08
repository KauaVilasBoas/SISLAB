using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Administration;

/// <summary>
/// Endpoints administrativos de gestão de membros da <b>company ativa</b>.
///
/// <para>Este é um controller <b>MVC</b> (e não Minimal API) por um motivo arquitetural preciso:
/// a <i>discovery</i> de permissões da Lumen (<c>PermissionDiscoveryScanner</c>) varre apenas
/// <c>ControllerActionDescriptor</c>s — endpoints Minimal API são invisíveis a ela. Ao decorar
/// as ações com <see cref="RequirePermissionAttribute"/>, a Lumen, no boot, materializa os codes
/// como <c>Permission</c> e os reconcilia no profile <c>Administrator</c>.</para>
///
/// <para><b>Convenção de code (imposta pela Lumen 1.1.0):</b> a permissão persistida é
/// SEMPRE <c>&lt;Controller&gt;.&lt;Action&gt;</c> — <c>Permission.Create</c> recomputa o code a
/// partir do nome do controller (sem o sufixo <c>Controller</c>) e do nome do método, IGNORANDO
/// qualquer string passada ao atributo. Por isso usamos <c>[RequirePermission]</c> <b>sem</b> code
/// explícito: assim a discovery (que grava <c>Controller.Action</c>) e o enforcement (que, com
/// code nulo no atributo, também deriva <c>Controller.Action</c> do <c>ControllerActionDescriptor</c>)
/// concordam. Passar um code explícito quebraria o enforcement — o handler compararia o code do
/// atributo contra o <c>Controller.Action</c> gravado e negaria (403) sempre. Os nomes de método
/// (<c>ListMembers</c>, <c>CheckRemovalEligibility</c>) são a fonte única dos codes; ver
/// <c>SISLAB.Modules.Identity.Contracts.Authorization.IdentityPermissions</c>.</para>
///
/// <para><b>Tenant-scoped:</b> toda ação opera sobre <see cref="ITenantContext.CompanyId"/>
/// (a company ativa resolvida do cookie httpOnly). O <c>PermissionAuthorizationHandler</c> da
/// Lumen resolve a permissão do usuário <i>dentro do escopo</i> dessa company (via
/// <c>ITenantScopeAccessor</c> → <c>SislabTenantScopeAccessor</c>): ter o profile Administrator
/// na LAFTE concede acesso quando a LAFTE está ativa, e nega (403) quando outra company está ativa.</para>
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
    /// Lista os membros da company ativa. Exige a permissão <c>CompanyMembers.ListMembers</c>
    /// <b>no escopo da company ativa</b>.
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
    /// Verifica (dry-run) a elegibilidade de remoção de um membro da company ativa.
    /// Exige a permissão <c>CompanyMembers.CheckRemovalEligibility</c>
    /// <b>no escopo da company ativa</b>.
    ///
    /// <para>Existe como <i>segunda</i> permissão decorada (escrita) para comprovar que a discovery
    /// materializa e reconcilia mais de um code. A mutação/persistência efetiva de membros entra
    /// num Command dedicado (CQRS write-side) num card posterior; aqui o foco é exclusivamente o
    /// enforcement da permissão de gestão.</para>
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
            title: "Nenhuma company ativa",
            detail: "Selecione uma empresa ativa via POST /api/companies/{companyId}/activate.",
            statusCode: StatusCodes.Status404NotFound);
}
