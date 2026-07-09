namespace SISLAB.Modules.Identity.Contracts.ActiveCompany;

/// <summary>
/// Public flattened DTO of a company the authenticated user belongs to.
/// Returned by the API for the SPA to build the active company selector.
/// Does not expose the Company aggregate or any internal Domain types.
/// </summary>
/// <param name="Id">Company identifier.</param>
/// <param name="Name">Company display name.</param>
public sealed record CompanyMembershipDto(Guid Id, string Name);
