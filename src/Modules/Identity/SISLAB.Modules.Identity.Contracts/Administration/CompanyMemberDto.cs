namespace SISLAB.Modules.Identity.Contracts.Administration;

/// <summary>
/// DTO público (achatado) de um membro de uma company, exposto pelos endpoints de
/// administração. Referencia o usuário da Lumen apenas por valor (<see cref="UserId"/>),
/// sem expor o aggregate <c>CompanyMembership</c> nem tipos internos do Domain.
/// </summary>
/// <param name="MembershipId">Identificador do vínculo (CompanyMembership).</param>
/// <param name="UserId">Identificador do usuário na Lumen (por valor).</param>
public sealed record CompanyMemberDto(Guid MembershipId, Guid UserId);
