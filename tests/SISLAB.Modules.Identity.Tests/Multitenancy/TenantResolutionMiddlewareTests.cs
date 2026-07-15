using System.Security.Claims;
using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;

namespace SISLAB.Modules.Identity.Tests.Multitenancy;

/// <summary>
/// Testes do <see cref="TenantResolutionMiddleware"/> (Opção A: company ativa em cookie httpOnly,
/// validada a cada request contra company_user).
///
/// Cobertura exigida pelo card #10:
/// - sem cookie → CompanyId permanece vazio;
/// - cookie de company NÃO pertencente → CompanyId permanece vazio (rejeitado);
/// - cookie válido de company pertencente → CompanyId preenchido.
/// </summary>
public sealed class TenantResolutionMiddlewareTests
{
    private const string CookieName = "sislab_active_company";

    [Fact]
    public async Task SemCookie_NaoDefineCompany()
    {
        var userId = Guid.NewGuid();
        var (context, tenant) = BuildContext(authenticatedUserId: userId, cookieCompanyId: null);
        var repo = new FakeCompanyRepository(memberCompanyId: Guid.NewGuid(), memberUserId: userId);
        bool nextCalled = false;

        await InvokeAsync(context, tenant, userId, repo, () => nextCalled = true);

        Assert.Equal(Guid.Empty, tenant.CompanyId);
        Assert.False(tenant.HasCompany);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task CookieDeCompanyNaoPertencente_NaoDefineCompany()
    {
        var userId = Guid.NewGuid();
        var foreignCompany = Guid.NewGuid();
        var (context, tenant) = BuildContext(authenticatedUserId: userId, cookieCompanyId: foreignCompany);
        // Usuário é membro de OUTRA company, não da que está no cookie.
        var repo = new FakeCompanyRepository(memberCompanyId: Guid.NewGuid(), memberUserId: userId);
        bool nextCalled = false;

        await InvokeAsync(context, tenant, userId, repo, () => nextCalled = true);

        Assert.Equal(Guid.Empty, tenant.CompanyId);
        Assert.False(tenant.HasCompany);
        Assert.True(nextCalled); // middleware nunca aborta a request
    }

    [Fact]
    public async Task CookieValidoDeCompanyPertencente_DefineCompany()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var (context, tenant) = BuildContext(authenticatedUserId: userId, cookieCompanyId: companyId);
        var repo = new FakeCompanyRepository(memberCompanyId: companyId, memberUserId: userId);
        bool nextCalled = false;

        await InvokeAsync(context, tenant, userId, repo, () => nextCalled = true);

        Assert.Equal(companyId, tenant.CompanyId);
        Assert.True(tenant.HasCompany);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task UsuarioNaoAutenticado_NaoDefineCompany()
    {
        var companyId = Guid.NewGuid();
        var (context, tenant) = BuildContext(authenticatedUserId: null, cookieCompanyId: companyId);
        var repo = new FakeCompanyRepository(memberCompanyId: companyId, memberUserId: Guid.NewGuid());

        await InvokeAsync(context, tenant, Guid.Empty, repo, () => { });

        Assert.Equal(Guid.Empty, tenant.CompanyId);
        Assert.False(repo.WasQueried); // nem consulta o repositório sem principal
    }

    [Fact]
    public async Task CookieComGuidInvalido_NaoDefineCompany()
    {
        var userId = Guid.NewGuid();
        var (context, tenant) = BuildContext(authenticatedUserId: userId, cookieCompanyId: null);
        context.Request.Headers.Cookie = $"{CookieName}=not-a-guid";
        var repo = new FakeCompanyRepository(memberCompanyId: Guid.NewGuid(), memberUserId: userId);

        await InvokeAsync(context, tenant, userId, repo, () => { });

        Assert.Equal(Guid.Empty, tenant.CompanyId);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task InvokeAsync(
        HttpContext context,
        TenantContext tenant,
        Guid userId,
        FakeCompanyRepository repo,
        Action onNext)
    {
        var accessor = new FakeUserIdAccessor(userId);
        var middleware = new TenantResolutionMiddleware(
            next: _ => { onNext(); return Task.CompletedTask; },
            logger: NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(context, tenant, accessor, repo);
    }

    private static (HttpContext Context, TenantContext Tenant) BuildContext(
        Guid? authenticatedUserId,
        Guid? cookieCompanyId)
    {
        var context = new DefaultHttpContext();

        if (authenticatedUserId is not null)
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString()) };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        }

        if (cookieCompanyId is not null)
            context.Request.Headers.Cookie = $"{CookieName}={cookieCompanyId.Value}";

        var services = new ServiceCollection().BuildServiceProvider();
        context.RequestServices = services;

        return (context, new TenantContext());
    }

    private sealed class FakeUserIdAccessor : IUserIdAccessor
    {
        private readonly Guid _userId;
        public FakeUserIdAccessor(Guid userId) => _userId = userId;

        public bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
        {
            userId = _userId;
            return _userId != Guid.Empty;
        }
    }

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        private readonly Guid _memberCompanyId;
        private readonly Guid _memberUserId;

        public FakeCompanyRepository(Guid memberCompanyId, Guid memberUserId)
        {
            _memberCompanyId = memberCompanyId;
            _memberUserId = memberUserId;
        }

        public bool WasQueried { get; private set; }

        public Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default)
        {
            WasQueried = true;
            bool isMember = companyId == _memberCompanyId && lumenUserId == _memberUserId;
            return Task.FromResult(isMember);
        }

        public Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Company>>([]);

        public Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Company?>(null);

        public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Company>>([]);

        public Task AddAsync(Company company, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Company company, CancellationToken ct = default) => Task.CompletedTask;
    }
}
