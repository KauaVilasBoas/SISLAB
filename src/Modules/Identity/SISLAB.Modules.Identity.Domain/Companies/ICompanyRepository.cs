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

    /// <summary>Persiste uma nova empresa no banco de dados.</summary>
    Task AddAsync(Company company, CancellationToken ct = default);

    /// <summary>Atualiza os dados de uma empresa existente.</summary>
    Task UpdateAsync(Company company, CancellationToken ct = default);
}
