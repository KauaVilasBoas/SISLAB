using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Catalogue that maps each SISLAB business <see cref="Role"/> to the Lumen authorization
/// <c>Profile</c> that backs it (card [E12] #77d).
///
/// <para><b>One profile per role, permissions from the map, scope on the assignment.</b> A Lumen
/// <c>Profile</c> is a global object (name + permissions) with no scope of its own — scoping is a
/// property of the <c>UserProfile</c> assignment (<c>ScopeId = companyId</c>). SISLAB therefore keeps
/// a single profile per role (named here), each seeded with the role's write permissions from
/// <see cref="RolePermissionsMap"/>, and achieves per-tenant isolation by assigning that profile to a
/// user <i>scoped to the active company</i>. This mirrors the pattern already proven by the LAFTE dev
/// seed (Administrator profile assigned scoped to a company) and avoids a profile-row explosion of
/// (companies × roles) with no isolation benefit — the scope lives on the assignment, not the profile.</para>
///
/// <para>Profile names are stable, prefixed to avoid colliding with Lumen's own system profiles
/// (<c>Administrator</c>/<c>User</c>). Renaming a value here would orphan the previously provisioned
/// profile, so the names are treated as an append-only contract.</para>
/// </summary>
public static class SislabRoleProfiles
{
    /// <summary>Prefix that namespaces SISLAB role profiles apart from Lumen's system profiles.</summary>
    public const string NamePrefix = "SISLAB.";

    /// <summary>Stable profile name for <paramref name="role"/> (e.g. <c>SISLAB.Coordinator</c>).</summary>
    public static string NameFor(Role role) => NamePrefix + role;

    /// <summary>Human-readable description persisted on the profile, for the Lumen backoffice.</summary>
    public static string DescriptionFor(Role role) =>
        $"SISLAB {role} role — write permissions granted to the {role} of a laboratory (company-scoped).";

    /// <summary>All roles that get a provisioned profile, in privilege order.</summary>
    public static IReadOnlyList<Role> AllRoles { get; } = Enum.GetValues<Role>();
}
