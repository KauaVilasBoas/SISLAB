using Lumen.Authorization.Application.Profiles.Create;
using Lumen.Authorization.Application.Profiles.SetPermissions;
using Lumen.Authorization.Application.Profiles.Update;
using Lumen.Authorization.Application.Queries;
using Lumen.Authorization.Application.UserProfiles.Assign;
using Lumen.Authorization.Application.UserProfiles.Remove;
using MediatR;
using SISLAB.Modules.Identity.Contracts.Authorization;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Lumen-backed implementation of <see cref="ILumenAuthorizationGateway"/> (card [E12] #101): the single
/// adapter that dispatches Lumen's authorization use cases through its MediatR pipeline and translates the
/// Lumen result records into SISLAB <see cref="Contracts.Authorization">Contracts</see> DTOs.
///
/// <para>Uses the Lumen application API exclusively — no direct EF/Dapper access to the <c>Lumen</c> schema is
/// needed, because every capability the profile-management screen requires (list permissions grouped, get a
/// profile's granted permissions, create/update a profile, reconcile a profile's permissions, assign/remove a
/// company-scoped user profile) is exposed as a Lumen MediatR request. This keeps SISLAB decoupled from
/// Lumen's persistence: it depends on Lumen's stable request/result contract, not its table shapes.</para>
///
/// <para>The injected <see cref="IMediator"/> is <b>MediatR's</b> dispatcher (registered by
/// <c>AddLumenAuthorization</c>), deliberately distinct from SISLAB's own
/// <see cref="SISLAB.SharedKernel.Messaging.IMediator"/>. Confining MediatR to this Infrastructure adapter is
/// what lets the rest of the module — controllers and SISLAB handlers — stay MediatR-free.</para>
/// </summary>
internal sealed class LumenAuthorizationGateway : ILumenAuthorizationGateway
{
    private readonly IMediator _lumenMediator;

    public LumenAuthorizationGateway(IMediator lumenMediator) => _lumenMediator = lumenMediator;

    public async Task<IReadOnlyList<PermissionGroupDto>> GetPermissionsGroupedAsync(
        Guid? selectedProfileId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ListPermissionsGroupResult> groups =
            await _lumenMediator.Send(new ListPermissionsQuery(), cancellationToken);

        HashSet<Guid> selectedIds = await ResolveSelectedPermissionIdsAsync(selectedProfileId, cancellationToken);

        return groups
            .Select(group => new PermissionGroupDto(
                group.GroupId,
                group.GroupName,
                group.Permissions
                    .Select(permission => new PermissionOptionDto(
                        permission.Id,
                        permission.Code,
                        permission.DisplayName,
                        selectedIds.Contains(permission.Id)))
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ProfileDto>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ListProfilesResult> profiles =
            await _lumenMediator.Send(new ListProfilesQuery(), cancellationToken);

        return profiles
            .Select(profile => new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsSystem))
            .ToList();
    }

    public async Task<ProfileDto?> FindProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        GetProfileResult? profile = await _lumenMediator.Send(new GetProfileQuery(profileId), cancellationToken);
        return profile is null
            ? null
            : new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsSystem);
    }

    public async Task<Guid> CreateProfileAsync(
        string name,
        string description,
        CancellationToken cancellationToken = default)
    {
        CreateProfileResult result =
            await _lumenMediator.Send(new CreateProfileCommand(name, description), cancellationToken);
        return result.Id;
    }

    public Task UpdateProfileAsync(
        Guid profileId,
        string name,
        string description,
        CancellationToken cancellationToken = default)
        => _lumenMediator.Send(new UpdateProfileCommand(profileId, name, description), cancellationToken);

    public Task SetProfilePermissionsAsync(
        Guid profileId,
        IReadOnlyList<Guid> permissionIds,
        string? actorUsername,
        CancellationToken cancellationToken = default)
        => _lumenMediator.Send(
            new SetProfilePermissionsCommand(profileId, permissionIds, actorUsername),
            cancellationToken);

    public Task AssignProfileAsync(
        Guid userId,
        Guid profileId,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => _lumenMediator.Send(
            new AssignUserProfileCommand(userId, profileId, companyId),
            cancellationToken);

    public Task RemoveProfileAsync(
        Guid userId,
        Guid profileId,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => _lumenMediator.Send(
            new RemoveUserProfileCommand(userId, profileId, companyId),
            cancellationToken);

    /// <summary>
    /// Resolves the set of permission ids already granted to the profile the query is scoped to, or an empty
    /// set when no profile was requested (a new profile has nothing selected).
    /// </summary>
    private async Task<HashSet<Guid>> ResolveSelectedPermissionIdsAsync(
        Guid? selectedProfileId,
        CancellationToken cancellationToken)
    {
        if (selectedProfileId is not { } profileId)
            return [];

        GetProfileResult? profile = await _lumenMediator.Send(new GetProfileQuery(profileId), cancellationToken);
        return profile is null ? [] : [.. profile.PermissionIds];
    }
}
