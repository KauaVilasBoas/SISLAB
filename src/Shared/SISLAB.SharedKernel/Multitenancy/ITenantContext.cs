namespace SISLAB.SharedKernel.Multitenancy;

/// <summary>
/// Contexto de tenant da requisição atual (Opção A: companyId FORA do JWT).
/// A company ativa é resolvida a partir de um cookie httpOnly de company, validado a cada
/// request contra <c>company_user</c> pelo TenantResolutionMiddleware. Scoped — uma
/// instância por requisição HTTP.
/// CompanyId flui: cookie de company ativa → middleware valida em company_user →
/// ITenantContext → Command → global query filter EF → WHERE no Dapper.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// ID da empresa (tenant) que está executando a operação.
    /// Em rotas públicas, sem cookie válido ou quando o usuário não pertence à company,
    /// permanece <see cref="System.Guid.Empty"/> (sem tenant ativo).
    /// </summary>
    Guid CompanyId { get; }
}
