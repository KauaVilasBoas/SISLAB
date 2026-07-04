namespace SISLAB.SharedKernel.Multitenancy;

/// <summary>
/// Contexto de tenant resolvido a partir do JWT da requisição atual.
/// Scoped — uma instância por requisição HTTP.
/// CompanyId flui: JWT → ITenantContext → Command → global query filter EF → WHERE no Dapper.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// ID da empresa (tenant) que está executando a operação.
    /// Nunca nulo em rotas autenticadas — middleware garante o preenchimento.
    /// </summary>
    Guid CompanyId { get; }
}
