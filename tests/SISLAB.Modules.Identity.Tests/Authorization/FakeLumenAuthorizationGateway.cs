using SISLAB.Modules.Identity.Contracts.Authorization;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Hand-rolled fake of <see cref="ILumenAuthorizationGateway"/> for SISLAB handler tests: it records the
/// arguments each method was called with and returns pre-seeded responses, so a handler can be exercised
/// without Lumen, MediatR or a database. Only the members a given test needs are seeded; the rest return
/// benign defaults.
/// </summary>
internal sealed class FakeLumenAuthorizationGateway : ILumenAuthorizationGateway
{
    public Guid? LastSelectedProfileId { get; private set; }
    public IReadOnlyList<PermissionGroupDto> GroupsToReturn { get; set; } = [];

    public (string Name, string Description)? CreatedProfile { get; private set; }
    public Guid CreatedProfileId { get; set; } = Guid.NewGuid();

    public (Guid ProfileId, string Name, string Description)? UpdatedProfile { get; private set; }

    public (Guid ProfileId, IReadOnlyList<Guid> PermissionIds, string? Actor)? SetPermissionsCall { get; private set; }

    public (Guid UserId, Guid ProfileId, Guid CompanyId)? AssignCall { get; private set; }
    public (Guid UserId, Guid ProfileId, Guid CompanyId)? RemoveCall { get; private set; }

    public ProfileDto? ProfileToReturn { get; set; }
    public IReadOnlyList<ProfileDto> ProfilesToReturn { get; set; } = [];

    public Task<IReadOnlyList<PermissionGroupDto>> GetPermissionsGroupedAsync(
        Guid? selectedProfileId,
        CancellationToken cancellationToken = default)
    {
        LastSelectedProfileId = selectedProfileId;
        return Task.FromResult(GroupsToReturn);
    }

    public Task<IReadOnlyList<ProfileDto>> ListProfilesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProfilesToReturn);

    public Task<ProfileDto?> FindProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
        => Task.FromResult(ProfileToReturn);

    public Task<Guid> CreateProfileAsync(string name, string description, CancellationToken cancellationToken = default)
    {
        CreatedProfile = (name, description);
        return Task.FromResult(CreatedProfileId);
    }

    public Task UpdateProfileAsync(Guid profileId, string name, string description, CancellationToken cancellationToken = default)
    {
        UpdatedProfile = (profileId, name, description);
        return Task.CompletedTask;
    }

    public Task SetProfilePermissionsAsync(
        Guid profileId,
        IReadOnlyList<Guid> permissionIds,
        string? actorUsername,
        CancellationToken cancellationToken = default)
    {
        SetPermissionsCall = (profileId, permissionIds, actorUsername);
        return Task.CompletedTask;
    }

    public Task AssignProfileAsync(Guid userId, Guid profileId, Guid companyId, CancellationToken cancellationToken = default)
    {
        AssignCall = (userId, profileId, companyId);
        return Task.CompletedTask;
    }

    public Task RemoveProfileAsync(Guid userId, Guid profileId, Guid companyId, CancellationToken cancellationToken = default)
    {
        RemoveCall = (userId, profileId, companyId);
        return Task.CompletedTask;
    }

    /// <summary>Clears the recorded assign call, so a test can assert that a later step performs no new assignment.</summary>
    public void ResetAssignCall() => AssignCall = null;
}
