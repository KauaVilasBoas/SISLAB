namespace SISLAB.Modules.Identity.Contracts.Administration;

/// <summary>
/// Public read port (Anti-Corruption seam, §2) that answers membership questions about the tenancy store the
/// Identity module owns, for <b>other</b> modules that must validate a user against the active company without
/// reaching into Identity's Domain/Infrastructure.
///
/// <para>Experiments uses it to guarantee that a user being assigned as a responsible (experiment lead or step)
/// actually belongs to the active company — the tenancy link (<c>company_user</c>) is owned by Identity, so the
/// check crosses the module boundary only through this Contracts port, never a cross-store query or FK.</para>
/// </summary>
public interface ICompanyMembershipQuery
{
    /// <summary>
    /// Whether <paramref name="userId"/> (a Lumen user id) is an <b>active</b> member of the company
    /// <paramref name="companyId"/>. Returns false for a non-member, an inactive company or an unknown user.
    /// </summary>
    Task<bool> IsActiveMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
}
