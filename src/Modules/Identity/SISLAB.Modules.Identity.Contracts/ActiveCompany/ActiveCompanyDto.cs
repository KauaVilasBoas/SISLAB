namespace SISLAB.Modules.Identity.Contracts.ActiveCompany;

/// <summary>
/// Public DTO for the active company resolved for the current request (from the httpOnly cookie,
/// re-validated against <c>company_memberships</c> by TenantResolutionMiddleware).
/// Returned by the API so the SPA can confirm which company is currently active.
/// </summary>
/// <param name="CompanyId">Identifier of the active company (tenant) for the request.</param>
public sealed record ActiveCompanyDto(Guid CompanyId);
