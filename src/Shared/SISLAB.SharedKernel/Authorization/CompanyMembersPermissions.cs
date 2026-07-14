namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for member administration of the active company
/// (<c>CompanyMembersController</c> → prefix <c>CompanyMembers</c>).
/// See <see cref="InventoryPermissions"/> for the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention.
///
/// <para>This is the single source of truth for the CompanyMembers codes; the module-facing
/// <c>SISLAB.Modules.Identity.Contracts.Authorization.IdentityPermissions.CompanyMembers</c>
/// re-exports these constants so existing consumers keep compiling.</para>
/// </summary>
public static class CompanyMembersPermissions
{
    /// <summary>List members of the active company (GET <c>ListMembers</c>). Read — not write-gated.</summary>
    public const string ListMembers = "CompanyMembers.ListMembers";

    /// <summary>Dry-run a member's removal eligibility (GET <c>CheckRemovalEligibility</c>). Read.</summary>
    public const string CheckRemovalEligibility = "CompanyMembers.CheckRemovalEligibility";

    /// <summary>
    /// Change a member's business role (PUT <c>ChangeMemberRole</c>) — a management write permission
    /// held only by the Coordinator. Materialized by Lumen discovery once the action exists (card #77e),
    /// at which point it joins <see cref="All"/> and thus the Coordinator profile.
    /// </summary>
    public const string ChangeMemberRole = "CompanyMembers.ChangeMemberRole";

    /// <summary>
    /// Every CompanyMembers <b>write</b> permission (management actions). Empty until the
    /// <c>ChangeMemberRole</c> action is introduced by card #77e — a permission constant only joins the
    /// write set once a real controller action backs it (anti-drift).
    /// </summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>();
}
