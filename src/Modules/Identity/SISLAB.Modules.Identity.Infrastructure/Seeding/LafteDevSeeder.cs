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
/// Idempotent seeder for the SISLAB demo environment: demo companies + admin user.
///
/// Ensures — idempotently (re-runs do not duplicate) — the minimum state to run E1
/// and prove tenant-scoped authorization enforcement (#12):
/// 1. Admin user in Lumen Identity, created already active (<c>ConfirmEmail</c> — no dependency
///    on the email confirmation flow, which is broken in package 1.0.0).
/// 2. Company <c>LAFTE</c> (SISLAB aggregate, deterministic Id) — <b>allow</b>:
///    admin is a member AND receives the <c>Administrator</c> profile
///    <b>scoped to LAFTE</b> (<c>ScopeId = companyId</c>).
/// 3. Company <c>ACME</c> (SISLAB aggregate, deterministic Id) — <b>deny</b>:
///    admin is a member but has NO Administrator profile. With ACME active,
///    <c>[RequirePermission]</c>-protected endpoints return 403.
///
/// Persistence: each bounded context writes through its own DbContext — SISLAB tenancy via
/// <see cref="IdentityDbContext"/>, users via Lumen Identity's DbContext, and profiles via
/// Lumen Authorization's DbContext (both resolved by type from the container, as they are
/// internal to the package). No cross-boundary FK is ever touched.
/// </summary>
public sealed class LafteDevSeeder
{
    /// <summary>
    /// Deterministic id for the LAFTE demo company — fixed across restarts for idempotent
    /// existence checks and to match the manual seed documented in DEV_SETUP.md.
    /// </summary>
    public static readonly Guid LafteCompanyId = new("10000000-0000-0000-0000-00000000000a");

    /// <summary>
    /// Deterministic id for the ACME demo company, in which the admin is a member
    /// WITHOUT the Administrator profile. Exists solely to prove tenant-scoped enforcement:
    /// with ACME active the admin gets 403 on protected endpoints.
    /// </summary>
    public static readonly Guid AcmeCompanyId = new("10000000-0000-0000-0000-00000000000b");

    private const string LafteCompanyName = "LAFTE";
    private const string LafteTaxId = "00000000000191";

    private const string AcmeCompanyName = "ACME";
    private const string AcmeTaxId = "00000000000272";

    // Lumen's internal DbContexts, resolved by type (no public interface exposes them).
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
    /// Runs the idempotent seed. Each step checks for existence before creating.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!_options.HasAdminCredentials)
        {
            _logger.LogWarning(
                "LAFTE seed is enabled but 'Seed:Admin:*' credentials are incomplete. Seed skipped.");
            return;
        }

        _logger.LogInformation("Starting idempotent LAFTE + admin seed...");

        Guid adminUserId = await EnsureAdminUserAsync(ct);

        // Company WITH permission (allow): admin is a member AND receives Administrator profile scoped to LAFTE.
        Company lafte = await EnsureCompanyAsync(LafteCompanyId, LafteCompanyName, LafteTaxId, ct);
        await EnsureMembershipAsync(lafte, adminUserId, ct);
        await EnsureAdministratorProfileAsync(adminUserId, lafte.Id, ct);

        // Company WITHOUT permission (deny): admin is a member but has NO Administrator profile.
        // Proves tenant-scoped enforcement: with ACME active, protected endpoints return 403.
        Company acme = await EnsureCompanyAsync(AcmeCompanyId, AcmeCompanyName, AcmeTaxId, ct);
        await EnsureMembershipAsync(acme, adminUserId, ct);

        _logger.LogInformation(
            "Seed complete. LAFTE(allow)={LafteId}, ACME(deny)={AcmeId}, AdminUserId={AdminUserId}.",
            lafte.Id, acme.Id, adminUserId);
    }

    /// <summary>Ensures a demo company with a deterministic id. Idempotent by id.</summary>
    private async Task<Company> EnsureCompanyAsync(Guid companyId, string name, string taxId, CancellationToken ct)
    {
        Company? existing = await _companyRepository.FindByIdAsync(companyId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Company {Name} already exists (Id={CompanyId}).", name, existing.Id);
            return existing;
        }

        Company company = Company.Seed(companyId, name, taxId);
        await _companyRepository.AddAsync(company, ct);
        await _sislabDbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Company {Name} created (Id={CompanyId}).", name, company.Id);
        return company;
    }

    /// <summary>
    /// Ensures the admin user in Lumen Identity, already active. Idempotent by email.
    /// Creates via Lumen's domain (hash + <see cref="User.Create"/> + <see cref="User.ConfirmEmail"/>)
    /// to bypass the email confirmation flow (broken in package 1.0.0).
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
                _logger.LogInformation("Existing admin user activated (Id={UserId}).", existing.Id);
            }
            else
            {
                _logger.LogInformation("Admin user already exists and is active (Id={UserId}).", existing.Id);
            }

            return existing.Id;
        }

        string passwordHash = _passwordHasher.Hash(_options.Admin.Password);
        User admin = User.Create(email, _options.Admin.Username.Trim(), passwordHash);
        admin.ConfirmEmail(); // activates the user (IsActive=true) without the email step

        await _userRepository.InsertAsync(admin, ct);
        await SaveLumenIdentityAsync(ct);

        _logger.LogInformation("Admin user created and activated (Id={UserId}).", admin.Id);
        return admin.Id;
    }

    /// <summary>Ensures the admin ↔ company link in company_memberships. Idempotent.</summary>
    private async Task EnsureMembershipAsync(Company company, Guid adminUserId, CancellationToken ct)
    {
        bool alreadyMember = company.Memberships.Any(m => m.LumenUserId == adminUserId);
        if (alreadyMember)
        {
            _logger.LogInformation("Admin is already a member of {Name}.", company.Name);
            return;
        }

        company.AddMember(adminUserId);

        // Force the new CompanyMembership to the Added state explicitly.
        //
        // The membership PK is a client-generated Guid (CompanyMembership.Create). EF marks
        // Guid keys as ValueGeneratedOnAdd; when a child is reachable via navigation from an
        // already-tracked principal and already carries a PK value, EF interprets it as Modified
        // and emits an UPDATE (0 rows → DbUpdateConcurrencyException). Marking it Added makes
        // the INSERT explicit and keeps the seed robust.
        CompanyMembership newMembership = company.Memberships.First(m => m.LumenUserId == adminUserId);
        _sislabDbContext.Entry(newMembership).State = EntityState.Added;

        await _sislabDbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Admin ↔ {Name} membership created.", company.Name);
    }

    /// <summary>
    /// Ensures the Administrator profile is assigned to the admin, scoped to LAFTE
    /// (<c>ScopeId = companyId</c>). Idempotent via <see cref="IUserProfileRepository.FindActiveAsync"/>.
    /// </summary>
    private async Task EnsureAdministratorProfileAsync(Guid adminUserId, Guid companyId, CancellationToken ct)
    {
        Guid administratorProfileId = SystemProfiles.AdministratorId;

        UserProfile? existing = await _userProfileRepository.FindActiveAsync(
            adminUserId, administratorProfileId, companyId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Admin already has the Administrator profile (scope LAFTE).");
            return;
        }

        UserProfile assignment = UserProfile.Create(adminUserId, administratorProfileId, companyId);
        await _userProfileRepository.InsertAsync(assignment, ct);
        await SaveLumenAuthorizationAsync(ct);

        _logger.LogInformation("Administrator profile assigned to admin (scope LAFTE={CompanyId}).", companyId);
    }

    private Task SaveLumenIdentityAsync(CancellationToken ct)
        => ResolveDbContext(LumenIdentityDbContextType).SaveChangesAsync(ct);

    private Task SaveLumenAuthorizationAsync(CancellationToken ct)
        => ResolveDbContext(LumenAuthorizationDbContextType).SaveChangesAsync(ct);

    private DbContext ResolveDbContext(Type dbContextType)
        => (DbContext)_serviceProvider.GetRequiredService(dbContextType);
}
