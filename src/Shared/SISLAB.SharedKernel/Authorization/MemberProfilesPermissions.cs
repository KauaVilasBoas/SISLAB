namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for assigning/removing profiles to members of the active company
/// (<c>MemberProfilesController</c> → prefix <c>MemberProfiles</c>, card [E12] #104).
/// See <see cref="InventoryPermissions"/> for the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention.
///
/// <para>These codes gate who may manage a member's authorization profiles within the active company. The
/// assignment is company-scoped (<c>UserProfile.ScopeId = companyId</c>) and the handlers enforce that the
/// target is a member of the active company, so the capability never crosses tenant boundaries.</para>
///
/// <para>Single source of truth for the MemberProfiles codes; re-exported by
/// <c>SISLAB.Modules.Identity.Contracts.Authorization.IdentityPermissions.MemberProfiles</c>.</para>
/// </summary>
public static class MemberProfilesPermissions
{
    /// <summary>Assign a profile to a member of the active company (POST <c>AssignProfile</c>). Write.</summary>
    public const string AssignProfile = "MemberProfiles.AssignProfile";

    /// <summary>Remove a profile assignment from a member of the active company (DELETE <c>RemoveProfile</c>). Write.</summary>
    public const string RemoveProfile = "MemberProfiles.RemoveProfile";

    /// <summary>Every member-profile <b>write</b> permission (management actions).</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>
    {
        AssignProfile, RemoveProfile
    };
}
