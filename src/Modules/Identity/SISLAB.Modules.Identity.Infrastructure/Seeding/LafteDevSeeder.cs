using Lumen.Authorization;
using Lumen.Authorization.Domain;
using Lumen.Identity.Domain.Security;
using Lumen.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Infrastructure.Persistence;

namespace SISLAB.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Seeder idempotente do ambiente demo do SISLAB: empresas demo + usuário administrador.
///
/// Garante, de forma idempotente (reexecução não duplica), o estado mínimo para operar o E1
/// <b>e provar o enforcement tenant-scoped do #12</b>:
/// <list type="number">
///   <item>Usuário admin na Lumen Identity, criado <b>já ativo</b> (<c>ConfirmEmail</c> —
///         sem depender de confirmação por e-mail, quebrada no pacote 1.0.0).</item>
///   <item>Company <c>LAFTE</c> (agregado do SISLAB, Id determinístico) — <b>allow</b>:
///         admin é membro E recebe o profile <c>Administrator</c> <b>tenant-scoped à LAFTE</b>
///         (<c>ScopeId = companyId</c>).</item>
///   <item>Company <c>ACME</c> (agregado do SISLAB, Id determinístico) — <b>deny</b>:
///         admin é membro, porém <b>sem</b> o profile Administrator. Com ACME ativa, os
///         endpoints protegidos por <c>[RequirePermission]</c> retornam 403.</item>
/// </list>
///
/// <para>
/// Persistência: cada bounded context grava no seu próprio DbContext — SISLAB tenancy via
/// <see cref="IdentityDbContext"/>, usuários via o DbContext da Lumen Identity e perfis via o
/// DbContext da Lumen Authorization (ambos resolvidos por tipo do container, pois são internos
/// ao pacote). Nenhuma tabela cross-boundary é referenciada por FK.
/// </para>
/// </summary>
public sealed class LafteDevSeeder
{
    /// <summary>
    /// Id determinístico da empresa demo LAFTE. Fixo entre execuções para permitir a checagem
    /// de existência por Id (idempotência) e casar com o seed manual documentado no DEV_SETUP.
    /// </summary>
    public static readonly Guid LafteCompanyId = new("10000000-0000-0000-0000-00000000000a");

    /// <summary>
    /// Id determinístico de uma <b>segunda</b> empresa demo (ACME) na qual o admin é membro
    /// <b>sem</b> o profile Administrator. Existe exclusivamente para provar o enforcement
    /// tenant-scoped do #12: com ACME ativa, o admin recebe <b>403</b> nos endpoints protegidos,
    /// porque sua permissão Administrator está escopada apenas à LAFTE.
    /// </summary>
    public static readonly Guid AcmeCompanyId = new("10000000-0000-0000-0000-00000000000b");

    private const string LafteCompanyName = "LAFTE";
    private const string LafteTaxId = "00000000000191";

    private const string AcmeCompanyName = "ACME";
    private const string AcmeTaxId = "00000000000272";

    // DbContexts internos da Lumen, resolvidos por tipo (não há interface pública para eles).
    private static readonly Type LumenIdentityDbContextType =
        typeof(IUserRepository).Assembly.GetType("Lumen.Identity.Persistence.IdentityDbContext")!;

    private static readonly Type LumenAuthorizationDbContextType =
        typeof(IUserProfileRepository).Assembly.GetType("Lumen.Authorization.Persistence.LumenAuthorizationDbContext")!;

    private readonly ICompanyRepository _companyRepository;
    private readonly IdentityDbContext _sislabDbContext;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly DevSeedOptions _options;
    private readonly ILogger<LafteDevSeeder> _logger;

    public LafteDevSeeder(
        ICompanyRepository companyRepository,
        IdentityDbContext sislabDbContext,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUserProfileRepository userProfileRepository,
        IServiceProvider serviceProvider,
        DevSeedOptions options,
        ILogger<LafteDevSeeder> logger)
    {
        _companyRepository = companyRepository;
        _sislabDbContext = sislabDbContext;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _userProfileRepository = userProfileRepository;
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Executa o seed idempotente. Cada passo checa a existência antes de criar.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!_options.HasAdminCredentials)
        {
            _logger.LogWarning(
                "Seed LAFTE habilitado, mas credenciais 'Seed:Admin:*' incompletas. Seed ignorado.");
            return;
        }

        _logger.LogInformation("Iniciando seed idempotente LAFTE + admin...");

        Guid adminUserId = await EnsureAdminUserAsync(ct);

        // Company COM permissão (allow): admin é membro E recebe o profile Administrator (scope LAFTE).
        Company lafte = await EnsureCompanyAsync(LafteCompanyId, LafteCompanyName, LafteTaxId, ct);
        await EnsureMembershipAsync(lafte, adminUserId, ct);
        await EnsureAdministratorProfileAsync(adminUserId, lafte.Id, ct);

        // Company SEM permissão (deny): admin é membro, mas NÃO recebe o profile Administrator.
        // Prova o enforcement tenant-scoped: com ACME ativa, endpoints protegidos retornam 403.
        Company acme = await EnsureCompanyAsync(AcmeCompanyId, AcmeCompanyName, AcmeTaxId, ct);
        await EnsureMembershipAsync(acme, adminUserId, ct);

        _logger.LogInformation(
            "Seed concluído. LAFTE(allow)={LafteId}, ACME(deny)={AcmeId}, AdminUserId={AdminUserId}.",
            lafte.Id, acme.Id, adminUserId);
    }

    /// <summary>Garante uma empresa demo com Id determinístico. Idempotente por Id.</summary>
    private async Task<Company> EnsureCompanyAsync(Guid companyId, string name, string taxId, CancellationToken ct)
    {
        Company? existing = await _companyRepository.FindByIdAsync(companyId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Company {Name} já existe (Id={CompanyId}).", name, existing.Id);
            return existing;
        }

        Company company = Company.Seed(companyId, name, taxId);
        await _companyRepository.AddAsync(company, ct);
        await _sislabDbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Company {Name} criada (Id={CompanyId}).", name, company.Id);
        return company;
    }

    /// <summary>
    /// Garante o usuário admin na Lumen Identity, já ativo. Idempotente por e-mail.
    /// Cria via domínio da Lumen (hash + <see cref="User.Create"/> + <see cref="User.ConfirmEmail"/>)
    /// para não depender do fluxo de confirmação por e-mail (quebrado no pacote 1.0.0).
    /// </summary>
    private async Task<Guid> EnsureAdminUserAsync(CancellationToken ct)
    {
        string email = _options.Admin.Email.Trim();

        User? existing = await _userRepository.FindByEmailAsync(email, ct);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.ConfirmEmail();
                await _userRepository.UpdateAsync(existing, ct);
                await SaveLumenIdentityAsync(ct);
                _logger.LogInformation("Usuário admin existente ativado (Id={UserId}).", existing.Id);
            }
            else
            {
                _logger.LogInformation("Usuário admin já existe e está ativo (Id={UserId}).", existing.Id);
            }

            return existing.Id;
        }

        string passwordHash = _passwordHasher.Hash(_options.Admin.Password);
        User admin = User.Create(email, _options.Admin.Username.Trim(), passwordHash);
        admin.ConfirmEmail(); // ativa o usuário (IsActive=true) sem etapa de e-mail

        await _userRepository.InsertAsync(admin, ct);
        await SaveLumenIdentityAsync(ct);

        _logger.LogInformation("Usuário admin criado e ativado (Id={UserId}).", admin.Id);
        return admin.Id;
    }

    /// <summary>Garante o vínculo admin ↔ company em company_memberships. Idempotente.</summary>
    private async Task EnsureMembershipAsync(Company company, Guid adminUserId, CancellationToken ct)
    {
        bool alreadyMember = company.Memberships.Any(m => m.LumenUserId == adminUserId);
        if (alreadyMember)
        {
            _logger.LogInformation("Admin já é membro da {Name}.", company.Name);
            return;
        }

        company.AddMember(adminUserId);

        // Força o novo CompanyMembership para o estado Added explicitamente.
        //
        // A PK do membership é um Guid definido no cliente (CompanyMembership.Create). Pela
        // convenção do EF, chaves Guid são ValueGeneratedOnAdd; quando o filho é alcançado pela
        // navegação de um principal JÁ RASTREADO e já traz PK preenchida, o EF o interpreta como
        // Modified e emite UPDATE (0 linhas → DbUpdateConcurrencyException). Marcar como Added
        // deixa o INSERT explícito e mantém o seed idempotente/robusto.
        CompanyMembership newMembership = company.Memberships.First(m => m.LumenUserId == adminUserId);
        _sislabDbContext.Entry(newMembership).State = EntityState.Added;

        await _sislabDbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Vínculo admin ↔ {Name} criado.", company.Name);
    }

    /// <summary>
    /// Garante o profile Administrator atribuído ao admin, tenant-scoped à LAFTE
    /// (<c>ScopeId = companyId</c>). Idempotente via <see cref="IUserProfileRepository.FindActiveAsync"/>.
    /// </summary>
    private async Task EnsureAdministratorProfileAsync(Guid adminUserId, Guid companyId, CancellationToken ct)
    {
        Guid administratorProfileId = SystemProfiles.AdministratorId;

        UserProfile? existing = await _userProfileRepository.FindActiveAsync(
            adminUserId, administratorProfileId, companyId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Admin já possui o profile Administrator (scope LAFTE).");
            return;
        }

        UserProfile assignment = UserProfile.Create(adminUserId, administratorProfileId, companyId);
        await _userProfileRepository.InsertAsync(assignment, ct);
        await SaveLumenAuthorizationAsync(ct);

        _logger.LogInformation("Profile Administrator atribuído ao admin (scope LAFTE={CompanyId}).", companyId);
    }

    private Task SaveLumenIdentityAsync(CancellationToken ct)
        => ResolveDbContext(LumenIdentityDbContextType).SaveChangesAsync(ct);

    private Task SaveLumenAuthorizationAsync(CancellationToken ct)
        => ResolveDbContext(LumenAuthorizationDbContextType).SaveChangesAsync(ct);

    private DbContext ResolveDbContext(Type dbContextType)
        => (DbContext)_serviceProvider.GetRequiredService(dbContextType);
}
