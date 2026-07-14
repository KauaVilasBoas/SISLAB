namespace SISLAB.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Anti-corruption seam (Adapter/Facade) over Lumen's authorization application API (card [E12] #101).
///
/// <para>Authorization in SISLAB is owned entirely by Lumen, whose profile/permission/user-profile use cases
/// are exposed as MediatR requests inside the <c>Lumen.Authorization</c> package. This port is the ONE place
/// that dispatches those requests and translates Lumen's result records into SISLAB
/// <see cref="Contracts.Authorization">Contracts</see> DTOs. Every SISLAB profile-management handler depends
/// on this abstraction, never on Lumen or MediatR directly, so:</para>
/// <list type="bullet">
///   <item>Lumen/MediatR stay confined to the Identity module's Infrastructure (§8) behind a single seam;</item>
///   <item>handlers are unit-testable against a fake gateway (no MediatR pipeline, no database);</item>
///   <item>a future Lumen API change ripples through exactly one adapter, not every handler.</item>
/// </list>
///
/// <para>The gateway never applies tenant scoping by itself. Company scoping (<c>ScopeId = companyId</c>) and
/// membership checks live in the SISLAB command/query handlers that own that invariant; the gateway is a thin,
/// scope-agnostic translator of Lumen capabilities.</para>
/// </summary>
public interface ILumenAuthorizationGateway
{
    /// <summary>
    /// Lists every auto-discovered permission grouped by its Lumen <c>PermissionGroup</c> (card #102). When
    /// <paramref name="selectedProfileId"/> is provided, each permission already granted to that profile is
    /// flagged <see cref="PermissionOptionDto.Selected"/>; otherwise nothing is selected. Permissions are
    /// read-only — this method never creates or mutates the catalogue.
    /// </summary>
    Task<IReadOnlyList<PermissionGroupDto>> GetPermissionsGroupedAsync(
        Guid? selectedProfileId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists all active (non-deleted) profiles.</summary>
    Task<IReadOnlyList<ProfileDto>> ListProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a single profile by id, or <see langword="null"/> when it does not exist.</summary>
    Task<ProfileDto?> FindProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Creates a profile from a name and description and returns the new profile id (card #103).</summary>
    Task<Guid> CreateProfileAsync(string name, string description, CancellationToken cancellationToken = default);

    /// <summary>Renames/re-describes an existing profile (card #103).</summary>
    Task UpdateProfileAsync(Guid profileId, string name, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently reconciles a profile's permissions to exactly the supplied set (card #103): permissions
    /// present in <paramref name="permissionIds"/> but not yet granted are added, those granted but absent are
    /// removed. Re-sending the same set is a no-op. Lumen rejects overwriting system-profile permissions.
    /// </summary>
    Task SetProfilePermissionsAsync(
        Guid profileId,
        IReadOnlyList<Guid> permissionIds,
        string? actorUsername,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a profile to a user scoped to a company (<c>ScopeId = companyId</c>), idempotently (card #104).
    /// </summary>
    Task AssignProfileAsync(
        Guid userId,
        Guid profileId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a company-scoped profile assignment from a user (card #104). A no-op when the assignment does
    /// not exist in that scope.
    /// </summary>
    Task RemoveProfileAsync(
        Guid userId,
        Guid profileId,
        Guid companyId,
        CancellationToken cancellationToken = default);
}
