namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// Repositório do agregado <see cref="Company"/>.
/// Implementação concreta reside na Infrastructure do módulo.
/// </summary>
public interface ICompanyRepository
{
    /// <summary>Busca uma empresa pelo seu ID.</summary>
    Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Retorna todas as empresas ativas.</summary>
    Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Lista as empresas ativas às quais o usuário da Lumen pertence (via <c>company_user</c>),
    /// ordenadas por nome. Usado no pós-login para resolver as companies do usuário.
    /// </summary>
    Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default);

    /// <summary>
    /// Indica se o usuário da Lumen é membro da empresa informada e se ela está ativa.
    /// Usado pelo middleware de tenant e pela troca de company para validar a associação.
    /// </summary>
    Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default);

    /// <summary>Persiste uma nova empresa no banco de dados.</summary>
    Task AddAsync(Company company, CancellationToken ct = default);

    /// <summary>Atualiza os dados de uma empresa existente.</summary>
    Task UpdateAsync(Company company, CancellationToken ct = default);
}
