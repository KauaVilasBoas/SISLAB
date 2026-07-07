namespace SISLAB.Modules.Identity.Contracts.ActiveCompany;

/// <summary>
/// DTO público (achatado) de uma empresa à qual o usuário autenticado pertence.
/// Exposto pela API para o SPA montar a seleção de company ativa.
/// Não expõe o aggregate Company nem tipos internos do Domain.
/// </summary>
/// <param name="Id">Identificador da empresa.</param>
/// <param name="Name">Nome/razão social da empresa.</param>
public sealed record CompanyMembershipDto(Guid Id, string Name);
