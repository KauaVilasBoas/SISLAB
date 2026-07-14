namespace SISLAB.Modules.Identity.Contracts.Authorization;

/// <summary>
/// A group of permissions as surfaced to the profile-management UI (card [E12] #102). Mirrors a Lumen
/// <c>PermissionGroup</c> and carries the permissions that belong to it, already flagged as
/// <see cref="PermissionOptionDto.Selected"/> when the query was asked about a specific profile.
///
/// <para>Permissions are auto-discovered by Lumen from <c>&lt;Controller&gt;.&lt;Action&gt;</c> and are
/// read-only: SISLAB never creates or edits a permission, it only presents them as checkboxes grouped by
/// their group so the operator can compose a profile.</para>
/// </summary>
/// <param name="GroupId">
/// The Lumen group id, or <see langword="null"/> for the synthetic "ungrouped" bucket that holds
/// permissions with no <c>GroupPermissionId</c>.
/// </param>
/// <param name="GroupName">Human-readable group name (e.g. <c>"Inventory"</c>), never empty.</param>
/// <param name="Permissions">The permissions in this group, ordered by code; never null, possibly empty.</param>
public sealed record PermissionGroupDto(
    Guid? GroupId,
    string GroupName,
    IReadOnlyList<PermissionOptionDto> Permissions);
