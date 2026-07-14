namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for authorization-profile management
/// (<c>ProfilesController</c> → prefix <c>Profiles</c>, cards [E12] #102/#103).
/// See <see cref="InventoryPermissions"/> for the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention.
///
/// <para>These are the codes Lumen materializes from the decorated actions of the profile-management
/// controller and enforces via <c>[RequirePermission]</c>. Profiles and the permission catalogue are global
/// to the Lumen instance (not tenant-scoped); management is gated to whoever holds the code in the active
/// company (typically coordination). SISLAB never creates permissions — the catalogue is auto-discovered and
/// read-only — so there is no <c>Permission</c>-create code here.</para>
///
/// <para>This is the single source of truth for the Profiles codes; the module-facing
/// <c>SISLAB.Modules.Identity.Contracts.Authorization.IdentityPermissions.Profiles</c> re-exports them.</para>
/// </summary>
public static class ProfilesPermissions
{
    /// <summary>List the permission catalogue grouped for the checkboxes (GET <c>ListAvailablePermissions</c>). Read.</summary>
    public const string ListAvailablePermissions = "Profiles.ListAvailablePermissions";

    /// <summary>
    /// The permission-gated read actions of the profile-management controller. Documentation-only but still
    /// anti-drift: each code here must map to a real read action.
    /// </summary>
    public static IReadOnlySet<string> Reads { get; } = new HashSet<string>
    {
        ListAvailablePermissions
    };

    /// <summary>
    /// Every profile-management <b>write</b> permission (management actions). Populated when the write
    /// endpoints land (card #103); each code must map to a real write action.
    /// </summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>();
}
