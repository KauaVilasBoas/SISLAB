using Lumen.Authorization.Domain;
using Microsoft.Extensions.Logging;
using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Ensures the Lumen authorization <c>Profile</c> for a SISLAB <see cref="Role"/> exists and carries
/// exactly the write permissions the <see cref="RolePermissionsMap"/> grants that role (card [E12] #77d).
///
/// <para>Idempotent: the profile is looked up by its stable name (<see cref="SislabRoleProfiles.NameFor"/>)
/// and created only when absent; its permission links are reconciled to the desired set on every call
/// (missing links inserted, nothing removed here since roles only ever gain the mapped permissions).
/// Permissions are resolved by code against the <see cref="IPermissionRepository"/> — Lumen's startup
/// discovery materializes the permission rows from the decorated controllers, so a code with no matching
/// permission row yet (e.g. a brand-new endpoint on the very first boot) is skipped defensively and picked
/// up on the next reconciliation rather than crashing the operation.</para>
///
/// <para>Persistence spans Lumen's own authorization <c>DbContext</c> (resolved and saved by the caller,
/// which owns the unit of work). This provisioner only mutates the Lumen domain via its repositories; it
/// never calls <c>SaveChanges</c> — keeping the transaction boundary in one place.</para>
/// </summary>
internal sealed class RoleProfileProvisioner
{
    private readonly IProfileRepository _profileRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<RoleProfileProvisioner> _logger;

    public RoleProfileProvisioner(
        IProfileRepository profileRepository,
        IPermissionRepository permissionRepository,
        ILogger<RoleProfileProvisioner> logger)
    {
        _profileRepository = profileRepository;
        _permissionRepository = permissionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the profile for <paramref name="role"/> exists with its mapped permissions, returning its id.
    /// Does not persist — the caller commits the Lumen authorization <c>DbContext</c>.
    /// </summary>
    public async Task<Guid> EnsureProfileAsync(Role role, CancellationToken ct)
    {
        string profileName = SislabRoleProfiles.NameFor(role);

        Profile? profile = await _profileRepository.FindByNameAsync(profileName, ct);
        if (profile is null)
        {
            profile = Profile.Create(profileName, SislabRoleProfiles.DescriptionFor(role), isSystem: false);
            await _profileRepository.InsertAsync(profile, ct);
            _logger.LogInformation("Provisioned SISLAB role profile {ProfileName} (Id={ProfileId}).",
                profileName, profile.Id);
        }

        await ReconcilePermissionsAsync(profile.Id, role, ct);
        return profile.Id;
    }

    /// <summary>
    /// Adds any missing permission links so the profile grants every code in the role's map. Existing
    /// links are left untouched (idempotent); codes with no materialized permission row are skipped.
    /// </summary>
    private async Task ReconcilePermissionsAsync(Guid profileId, Role role, CancellationToken ct)
    {
        IReadOnlySet<string> desiredCodes = RolePermissionsMap.ForRole(role);
        if (desiredCodes.Count == 0)
            return; // ReadOnly grants no write permission — nothing to link.

        IReadOnlyList<PermissionProfile> existingLinks =
            await _profileRepository.GetActivePermissionProfilesByProfileIdAsync(profileId, ct);
        HashSet<Guid> alreadyLinked = existingLinks.Select(l => l.PermissionId).ToHashSet();

        List<PermissionProfile> toInsert = [];
        foreach (string code in desiredCodes)
        {
            Permission? permission = await _permissionRepository.FindByCodeAsync(code, ct);
            if (permission is null)
            {
                _logger.LogWarning(
                    "Permission code {Code} for role {Role} is not materialized yet; skipping (will be linked on next reconcile).",
                    code, role);
                continue;
            }

            if (alreadyLinked.Contains(permission.Id))
                continue;

            toInsert.Add(PermissionProfile.Create(permission.Id, profileId));
        }

        if (toInsert.Count > 0)
        {
            await _profileRepository.InsertPermissionProfilesAsync(toInsert, ct);
            _logger.LogInformation(
                "Linked {Count} permission(s) to SISLAB role profile {Role}.", toInsert.Count, role);
        }
    }
}
