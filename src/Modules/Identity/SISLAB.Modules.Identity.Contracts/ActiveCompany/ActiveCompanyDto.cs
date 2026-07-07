namespace SISLAB.Modules.Identity.Contracts.ActiveCompany;

/// <summary>
/// DTO público da company ativa resolvida para a requisição corrente (via cookie httpOnly,
/// revalidado contra <c>company_user</c> pelo TenantResolutionMiddleware).
/// Exposto pela API para o SPA confirmar qual empresa está ativa.
/// </summary>
/// <param name="CompanyId">Identificador da empresa ativa (tenant) da requisição.</param>
public sealed record ActiveCompanyDto(Guid CompanyId);
