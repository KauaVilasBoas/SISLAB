namespace SISLAB.Modules.Identity.Contracts.Authorization;

/// <summary>
/// A single selectable permission for the profile-management checkboxes (card [E12] #102). Corresponds to a
/// Lumen <c>Permission</c> auto-discovered from <c>&lt;Controller&gt;.&lt;Action&gt;</c>.
/// </summary>
/// <param name="Id">The Lumen permission id — the value sent back when setting a profile's permissions.</param>
/// <param name="Code">The permission code (<c>&lt;Controller&gt;.&lt;Action&gt;</c>), the enforcement key.</param>
/// <param name="DisplayName">Human-readable label for the checkbox.</param>
/// <param name="Selected">
/// True when this permission is already granted to the profile the query was scoped to. Always false when the
/// query carried no <c>profileId</c> (a fresh profile has nothing selected yet).
/// </param>
public sealed record PermissionOptionDto(
    Guid Id,
    string Code,
    string DisplayName,
    bool Selected);
