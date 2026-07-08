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
/// Seeder idempotente do ambiente demo do SISLAB: empresa <c>LAFTE</c> + usuário administrador.
///
/// Garante, de forma idempotente (reexecução não duplica), o estado mínimo para operar o E1:
/// <list type="number">
///   <item>Company <c>LAFTE</c> (agregado do SISLAB) com Id determinístico.</item>
///   <item>Usuário admin na Lumen Identity, criado <b>já ativo</b> (<c>ConfirmEmail</c> —
///         sem depender de confirmação por e-mail, quebrada no pacote 1.0.0).</item>
///   <item>Vínculo admin ↔ LAFTE em <c>company_memberships</c> (CompanyMembership).</item>
///   <item>Profile <c>Administrator</c> (semeado pela Lumen) atribuído ao admin,
///         <b>tenant-scoped à LAFTE</b> (<c>ScopeId = companyId</c>).</item>
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

    private const string LafteCompanyName = "LAFTE";
    private const string LafteTaxId = "00000000000191";

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

        Company lafte = await EnsureLafteCompanyAsync(ct);
        Guid adminUserId = await EnsureAdminUserAsync(ct);
        await EnsureMembershipAsync(lafte, adminUserId, ct);
        await EnsureAdministratorProfileAsync(adminUserId, lafte.Id, ct);

        _logger.LogInformation(
            "Seed LAFTE concluído. CompanyId={CompanyId}, AdminUserId={AdminUserId}.",
            lafte.Id, adminUserId);
    }

    /// <summary>Garante a empresa LAFTE (Id determinístico). Idempotente por Id.</summary>
    private async Task<Company> EnsureLafteCompanyAsync(CancellationToken ct)
    {
        Company? existing = await _companyRepository.FindByIdAsync(LafteCompanyId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Company LAFTE já existe (Id={CompanyId}).", existing.Id);
            return existing;
        }

        Company lafte = Company.Seed(LafteCompanyId, LafteCompanyName, LafteTaxId);
        await _companyRepository.AddAsync(lafte, ct);
        await _sislabDbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Company LAFTE criada (Id={CompanyId}).", lafte.Id);
        return lafte;
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

    /// <summary>Garante o vínculo admin ↔ LAFTE em company_memberships. Idempotente.</summary>
    private async Task EnsureMembershipAsync(Company lafte, Guid adminUserId, CancellationToken ct)
    {
        bool alreadyMember = lafte.Memberships.Any(m => m.LumenUserId == adminUserId);
        if (alreadyMember)
        {
            _logger.LogInformation("Admin já é membro da LAFTE.");
            return;
        }

        lafte.AddMember(adminUserId);
        await _companyRepository.UpdateAsync(lafte, ct);
        await _sislabDbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Vínculo admin ↔ LAFTE criado.");
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
