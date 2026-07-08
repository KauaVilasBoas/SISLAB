using System.Security.Claims;
using Lumen.Authorization.AspNetCore;
using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Authorization;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Prova o <b>coração do #12</b>: o enforcement de <c>[RequirePermission]</c> respeita o
/// <i>escopo</i> da company ativa. Exercita o pipeline real da Lumen — o
/// <see cref="PermissionAuthorizationHandler"/> + a bridge de escopo do SISLAB
/// (<see cref="SislabTenantScopeAccessor"/> sobre <see cref="TenantContext"/>) — sem depender
/// de banco, JWT ou HTTP.
///
/// <para>O único ponto que varia entre allow e deny é a company ativa: o <b>mesmo</b> usuário,
/// o <b>mesmo</b> permission code, muda apenas o <c>scopeId</c> que o handler passa a
/// <see cref="IUserPermissionService.HasPermissionAsync"/>. Um fake concede a permissão só no
/// escopo da LAFTE; com a LAFTE ativa o requisito é satisfeito (allow), com a ACME ativa não
/// (deny). É exatamente o que a Lumen faz em produção contra a base real de UserProfile/Permission.</para>
/// </summary>
public sealed class TenantScopedPermissionEnforcementTests
{
    private static readonly Guid AdminUserId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid LafteCompanyId = new("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid AcmeCompanyId = new("10000000-0000-0000-0000-00000000000b");

    [Fact]
    public async Task Enforcement_ComLafteAtiva_ConcedeAcesso()
    {
        AuthorizationHandlerContext context = await EvaluateAsync(
            activeCompanyId: LafteCompanyId,
            permissionCode: IdentityPermissions.Companies.Read);

        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Enforcement_ComAcmeAtiva_NegaAcesso()
    {
        AuthorizationHandlerContext context = await EvaluateAsync(
            activeCompanyId: AcmeCompanyId,
            permissionCode: IdentityPermissions.Companies.Read);

        // Mesmo usuário, mesma permissão — porém a permissão Administrator está escopada à LAFTE.
        // Com ACME ativa, o requisito não é satisfeito → o pipeline resultaria em 403.
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Enforcement_PermissaoManage_SegueOMesmoEscopo()
    {
        AuthorizationHandlerContext allow = await EvaluateAsync(
            LafteCompanyId, IdentityPermissions.Companies.Manage);
        AuthorizationHandlerContext deny = await EvaluateAsync(
            AcmeCompanyId, IdentityPermissions.Companies.Manage);

        Assert.True(allow.HasSucceeded);
        Assert.False(deny.HasSucceeded);
    }

    /// <summary>
    /// Monta o pipeline real de autorização da Lumen para a company ativa informada e avalia
    /// o requisito de permissão, devolvendo o contexto já processado.
    /// </summary>
    private static async Task<AuthorizationHandlerContext> EvaluateAsync(
        Guid activeCompanyId,
        string permissionCode)
    {
        // TenantContext real: representa a company ativa resolvida do cookie httpOnly.
        TenantContext tenantContext = new();
        tenantContext.SetCompany(activeCompanyId);

        // Bridge real do SISLAB: expõe a company ativa como scopeId de autorização da Lumen.
        ITenantScopeAccessor scopeAccessor = new SislabTenantScopeAccessor(tenantContext);

        // Fake do serviço de permissões: espelha o estado semeado — o admin possui a permissão
        // APENAS no escopo da LAFTE (onde tem o profile Administrator). Fora dele, não possui.
        IUserPermissionService permissionService = new ScopedPermissionServiceFake(
            userId: AdminUserId,
            grantedScopeId: LafteCompanyId);

        IUserIdAccessor userIdAccessor = new FixedUserIdAccessor(AdminUserId);

        PermissionAuthorizationHandler handler = new(permissionService, userIdAccessor, scopeAccessor);

        PermissionRequirement requirement = new(permissionCode);
        ClaimsPrincipal user = new(new ClaimsIdentity(authenticationType: "Test"));

        AuthorizationHandlerContext context = new([requirement], user, resource: null);
        await handler.HandleAsync(context);
        return context;
    }

    /// <summary>Concede a permissão somente quando o <c>scopeId</c> é o escopo autorizado.</summary>
    private sealed class ScopedPermissionServiceFake : IUserPermissionService
    {
        private readonly Guid _userId;
        private readonly Guid _grantedScopeId;

        public ScopedPermissionServiceFake(Guid userId, Guid grantedScopeId)
        {
            _userId = userId;
            _grantedScopeId = grantedScopeId;
        }

        public Task<bool> HasPermissionAsync(
            Guid userId, string permissionCode, Guid? scopeId = null,
            CancellationToken cancellationToken = default)
        {
            bool granted = userId == _userId && scopeId == _grantedScopeId;
            return Task.FromResult(granted);
        }

        public Task<HashSet<string>> GetPermissionsAsync(
            Guid userId, Guid? scopeId = null, CancellationToken cancellationToken = default)
        {
            HashSet<string> permissions = userId == _userId && scopeId == _grantedScopeId
                ? [IdentityPermissions.Companies.Read, IdentityPermissions.Companies.Manage]
                : [];
            return Task.FromResult(permissions);
        }
    }

    /// <summary>Resolve sempre o mesmo userId (o principal de teste não carrega o claim real).</summary>
    private sealed class FixedUserIdAccessor : IUserIdAccessor
    {
        private readonly Guid _userId;

        public FixedUserIdAccessor(Guid userId) => _userId = userId;

        public bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }
}
