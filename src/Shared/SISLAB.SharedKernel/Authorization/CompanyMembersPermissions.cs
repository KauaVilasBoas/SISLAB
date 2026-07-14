namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for member administration of the active company
/// (<c>CompanyMembersController</c> → prefix <c>CompanyMembers</c>).
/// See <see cref="InventoryPermissions"/> for the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention.
///
/// <para>These are the codes Lumen materializes from the decorated controller actions and enforces via
/// <c>[RequirePermission]</c>. Which members receive them in a given company is owned entirely by Lumen
/// (profiles assigned to the user, scoped to the company). SISLAB no longer maps roles to permissions —
/// this catalogue exists only to remove magic strings from tests and consumers of the codes.</para>
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
    /// Every CompanyMembers <b>write</b> permission (management actions). Currently empty: member
    /// administration exposes only read actions today. New write actions must add their code here so the
    /// permission-catalogue drift test ties the constant to a real controller action (anti-drift).
    /// </summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>();
}
