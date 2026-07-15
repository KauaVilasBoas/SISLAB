using Lumen.Authorization.Domain;
using Lumen.Identity.Domain.Security;
using Lumen.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Modules.Identity.Contracts.Onboarding;

namespace SISLAB.Modules.Identity.Infrastructure.Onboarding;

/// <summary>
/// Lumen-backed implementation of <see cref="ICompanyOnboardingGateway"/> (card [E12] #75a): the single
/// adapter that provisions a coordinator account and its company-scoped authorization for self-service signup,
/// using Lumen's domain API directly — the same proven path as the dev seeder.
///
/// <para>Both Lumen bounded contexts persist through their own internal DbContexts, which expose no public
/// interface, so they are resolved by type from the container (as elsewhere in this module). No cross-boundary
/// FK is ever touched; the coordinator's Lumen user id crosses into SISLAB's tenancy store only by value.</para>
///
/// <para>The coordinator account is created already active (<see cref="User.ConfirmEmail"/>) to bypass the
/// e-mail confirmation flow (broken in Lumen.Identity 1.0.0), so the coordinator can authenticate immediately
/// after signup — satisfying the card's "created coordinator can authenticate" criterion. Authorization is
/// granted by assigning the coordinator profile scoped to the new company; the profile is ensured by name and
/// created on first use, keeping the operation idempotent without depending on a fixed system-profile id
/// (Lumen.Authorization 3.0.0 seeds none).</para>
/// </summary>
internal sealed class LumenCompanyOnboardingGateway : ICompanyOnboardingGateway
{
    /// <summary>
    /// The profile that materializes the coordinator's authority over their own tenant. SISLAB models no
    /// roles: full access to a company is a company-scoped Lumen profile assignment. This is the same profile
    /// the dev seed grants its admin, so a signed-up coordinator gets the identical, well-known capability set.
    /// </summary>
    private const string CoordinatorProfileName = "Administrator";
    private const string CoordinatorProfileDescription = "Full access to a company's SISLAB capabilities.";

    // Lumen's internal DbContexts, resolved by type (no public interface exposes them) — same seam the dev
    // seeder uses to commit Lumen writes.
    private static readonly Type LumenIdentityDbContextType =
        typeof(IUserRepository).Assembly.GetType("Lumen.Identity.Persistence.IdentityDbContext")!;

    private static readonly Type LumenAuthorizationDbContextType =
        typeof(IUserProfileRepository).Assembly.GetType("Lumen.Authorization.Persistence.LumenAuthorizationDbContext")!;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IProfileRepository _profiles;
    private readonly IUserProfileRepository _userProfiles;
    private readonly IServiceProvider _serviceProvider;

    public LumenCompanyOnboardingGateway(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IProfileRepository profiles,
        IUserProfileRepository userProfiles,
        IServiceProvider serviceProvider)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _profiles = profiles;
        _userProfiles = userProfiles;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> CoordinatorEmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        User? existing = await _users.FindByEmailAsync(email.Trim(), cancellationToken);
        return existing is not null;
    }

    public async Task<Guid> CreateCoordinatorAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        string passwordHash = _passwordHasher.Hash(password);
        User coordinator = User.Create(email.Trim(), username.Trim(), passwordHash);
        coordinator.ConfirmEmail(); // activate without the (broken 1.0.0) e-mail confirmation flow

        await _users.InsertAsync(coordinator, cancellationToken);
        await SaveLumenIdentityAsync(cancellationToken);

        return coordinator.Id;
    }

    public async Task GrantCoordinatorAccessAsync(
        Guid coordinatorUserId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        Guid profileId = await EnsureCoordinatorProfileAsync(cancellationToken);

        UserProfile? existing = await _userProfiles.FindActiveAsync(
            coordinatorUserId, profileId, companyId, cancellationToken);
        if (existing is not null)
            return; // idempotent: coordinator already holds the profile in this company

        UserProfile assignment = UserProfile.Create(coordinatorUserId, profileId, companyId);
        await _userProfiles.InsertAsync(assignment, cancellationToken);
        await SaveLumenAuthorizationAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves the coordinator profile by name, creating it on first use. Idempotent: the name is the stable
    /// key (Lumen 3.0.0 exposes no fixed system-profile id).
    /// </summary>
    private async Task<Guid> EnsureCoordinatorProfileAsync(CancellationToken cancellationToken)
    {
        Profile? existing = await _profiles.FindByNameAsync(CoordinatorProfileName, cancellationToken);
        if (existing is not null)
            return existing.Id;

        Profile profile = Profile.Create(
            CoordinatorProfileName, CoordinatorProfileDescription, isSystem: true);
        await _profiles.InsertAsync(profile, cancellationToken);
        await SaveLumenAuthorizationAsync(cancellationToken);

        return profile.Id;
    }

    private Task SaveLumenIdentityAsync(CancellationToken cancellationToken)
        => ResolveDbContext(LumenIdentityDbContextType).SaveChangesAsync(cancellationToken);

    private Task SaveLumenAuthorizationAsync(CancellationToken cancellationToken)
        => ResolveDbContext(LumenAuthorizationDbContextType).SaveChangesAsync(cancellationToken);

    private DbContext ResolveDbContext(Type dbContextType)
        => (DbContext)_serviceProvider.GetRequiredService(dbContextType);
}
