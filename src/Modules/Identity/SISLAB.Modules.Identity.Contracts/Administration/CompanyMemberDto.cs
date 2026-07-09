namespace SISLAB.Modules.Identity.Contracts.Administration;

/// <summary>
/// Public flattened DTO for a company member, returned by admin endpoints.
/// References the Lumen user by value only (<see cref="UserId"/>) — does not expose
/// the <c>CompanyMembership</c> aggregate or any internal Domain types.
/// </summary>
/// <param name="MembershipId">Identifier of the membership link (CompanyMembership).</param>
/// <param name="UserId">Identifier of the Lumen user (by value).</param>
public sealed record CompanyMemberDto(Guid MembershipId, Guid UserId);
