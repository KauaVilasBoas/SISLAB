using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Permission code catalogue for the Identity bounded context, consumed by
/// Lumen's granular authorization (<c>[RequirePermission]</c> on admin controllers).
///
/// <para><b>Code convention (enforced by Lumen 1.1.0):</b> the code is always
/// <c>&lt;Controller&gt;.&lt;Action&gt;</c> (controller class name without the <c>Controller</c>
/// suffix, plus the action method name, both in PascalCase as in C#). <c>Permission.Create</c>
/// inside Lumen recomputes the code from controller + action and <b>ignores</b> any explicit
/// string passed to the attribute. Therefore controllers are decorated with
/// <c>[RequirePermission]</c> <i>without</i> an explicit code — discovery writes
/// <c>Controller.Action</c> and enforcement derives the same value, keeping them in sync.</para>
///
/// <para>Centralizing the codes here avoids magic strings in tests and consumers.
/// If a method is renamed, its code changes — update the corresponding constant.</para>
/// </summary>
public static class IdentityPermissions
{
    /// <summary>
    /// Permissions for the active-company member administration controller
    /// (<c>CompanyMembersController</c> → prefix <c>CompanyMembers</c>).
    /// </summary>
    public static class CompanyMembers
    {
        /// <summary>List members of the active company (action <c>ListMembers</c>). Scope: active company.</summary>
        public const string ListMembers = CompanyMembersPermissions.ListMembers;

        /// <summary>Check removal eligibility of a member (action <c>CheckRemovalEligibility</c>). Scope: active company.</summary>
        public const string CheckRemovalEligibility = CompanyMembersPermissions.CheckRemovalEligibility;

        /// <summary>
        /// Change a member's business role (action <c>ChangeMemberRole</c>) — a management/write
        /// permission held only by the Coordinator. Scope: active company.
        /// </summary>
        public const string ChangeMemberRole = CompanyMembersPermissions.ChangeMemberRole;
    }
}
