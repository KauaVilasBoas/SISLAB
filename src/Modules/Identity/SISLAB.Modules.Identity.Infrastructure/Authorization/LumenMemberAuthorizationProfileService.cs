using Lumen.Authorization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Lumen-backed implementation of <see cref="IMemberAuthorizationProfileService"/> (card [E12] #77d):
/// translates a member's SISLAB <see cref="Role"/> into the corresponding Lumen authorization profile
/// assignment, <b>scoped to the company</b>.
///
/// <para>Reconciliation is idempotent and self-healing:
/// <list type="number">
/// <item>Ensures the profile for the target role exists with its mapped permissions
/// (<see cref="RoleProfileProvisioner"/>).</item>
/// <item>Removes (soft-deletes) any <i>other</i> SISLAB role-profile assignment the user holds in this
/// same company, so a role change never leaves the user accumulating profiles from previous roles.</item>
/// <item>Assigns the target profile scoped to the company if not already active — the assignment's
/// <c>ScopeId = companyId</c> is what guarantees tenant isolation: the permissions apply only in this
/// company, never leaking into another.</item>
/// </list></para>
///
/// <para>Writes go through Lumen's own authorization <c>DbContext</c> (resolved by type from the container,
/// as it is internal to the package — the same technique the LAFTE dev seed uses). This service owns and
/// commits that unit of work; it is invoked synchronously by the ChangeMemberRole command handler so the
/// member's effective permissions reflect the new role as soon as the command returns.</para>
/// </summary>
internal sealed class LumenMemberAuthorizationProfileService : IMemberAuthorizationProfileService
{
    // Lumen's authorization DbContext is internal to the package; resolved by type, no public interface.
    private static readonly Type LumenAuthorizationDbContextType =
        typeof(IUserProfileRepository).Assembly.GetType(
            "Lumen.Authorization.Persistence.LumenAuthorizationDbContext")!;

    private readonly RoleProfileProvisioner _profileProvisioner;
    private readonly IProfileRepository _profileRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LumenMemberAuthorizationProfileService> _logger;

    public LumenMemberAuthorizationProfileService(
        RoleProfileProvisioner profileProvisioner,
        IProfileRepository profileRepository,
        IUserProfileRepository userProfileRepository,
        IServiceProvider serviceProvider,
        ILogger<LumenMemberAuthorizationProfileService> logger)
    {
        _profileProvisioner = profileProvisioner;
        _profileRepository = profileRepository;
        _userProfileRepository = userProfileRepository;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReconcileAsync(
        Guid lumenUserId, Guid companyId, Role role, CancellationToken cancellationToken = default)
    {
        // 1. Ensure the target role profile exists with the permissions the role's map grants.
        Guid targetProfileId = await _profileProvisioner.EnsureProfileAsync(role, cancellationToken);

        // 2. Identify the set of profile ids that belong to SISLAB roles, to detect stale assignments.
        HashSet<Guid> sislabRoleProfileIds = await ResolveSislabRoleProfileIdsAsync(cancellationToken);

        // 3. Load the user's current assignments and reconcile within this company only.
        IReadOnlyList<UserProfile> assignments =
            await _userProfileRepository.ListByUserIdAsync(lumenUserId, cancellationToken);

        bool alreadyAssigned = false;
        foreach (UserProfile assignment in assignments)
        {
            bool inThisCompany = assignment.ScopeId == companyId;
            bool isSislabRoleProfile = sislabRoleProfileIds.Contains(assignment.ProfileId);
            if (!inThisCompany || !isSislabRoleProfile)
                continue;

            if (assignment.ProfileId == targetProfileId)
            {
                alreadyAssigned = true; // already holds the right profile in this company — idempotent.
                continue;
            }

            // Stale profile from a previous role in this company — remove it.
            assignment.SoftDelete();
            await _userProfileRepository.UpdateAsync(assignment, cancellationToken);
            _logger.LogInformation(
                "Removed stale role profile {ProfileId} from user {UserId} in company {CompanyId}.",
                assignment.ProfileId, lumenUserId, companyId);
        }

        // 4. Assign the target profile scoped to the company if not already present.
        if (!alreadyAssigned)
        {
            UserProfile newAssignment = UserProfile.Create(lumenUserId, targetProfileId, companyId);
            await _userProfileRepository.InsertAsync(newAssignment, cancellationToken);
            _logger.LogInformation(
                "Assigned role profile {ProfileId} to user {UserId} scoped to company {CompanyId}.",
                targetProfileId, lumenUserId, companyId);
        }

        await SaveLumenAuthorizationAsync(cancellationToken);
    }

    /// <summary>Resolves the ids of the profiles backing SISLAB roles (those provisioned so far).</summary>
    private async Task<HashSet<Guid>> ResolveSislabRoleProfileIdsAsync(CancellationToken ct)
    {
        HashSet<Guid> ids = [];
        foreach (Role role in SislabRoleProfiles.AllRoles)
        {
            Profile? profile = await _profileRepository.FindByNameAsync(SislabRoleProfiles.NameFor(role), ct);
            if (profile is not null)
                ids.Add(profile.Id);
        }

        return ids;
    }

    private Task SaveLumenAuthorizationAsync(CancellationToken ct)
        => ((DbContext)_serviceProvider.GetRequiredService(LumenAuthorizationDbContextType))
            .SaveChangesAsync(ct);
}
