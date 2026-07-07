using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Implementação concreta e mutável de <see cref="ITenantContext"/>, com ciclo de vida Scoped
/// (uma instância por requisição HTTP).
///
/// O <see cref="TenantResolutionMiddleware"/> resolve a empresa ativa a partir do cookie
/// httpOnly de company, valida contra <c>company_user</c> e chama <see cref="SetCompany"/>.
/// Em rotas públicas ou sem cookie válido, o <see cref="CompanyId"/> permanece
/// <see cref="Guid.Empty"/> — o <see cref="SislabTenantScopeAccessor"/> trata esse caso
/// retornando escopo nulo (sem tenant ativo).
/// </summary>
internal sealed class TenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid CompanyId { get; private set; } = Guid.Empty;

    /// <summary>
    /// Indica se há uma empresa ativa resolvida para a requisição atual.
    /// </summary>
    public bool HasCompany => CompanyId != Guid.Empty;

    /// <summary>
    /// Define a empresa ativa da requisição. Chamado exclusivamente pelo
    /// <see cref="TenantResolutionMiddleware"/> após validar a associação do usuário.
    /// </summary>
    /// <param name="companyId">ID da empresa ativa (não pode ser vazio).</param>
    public void SetCompany(Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("O CompanyId ativo não pode ser vazio.", nameof(companyId));

        CompanyId = companyId;
    }
}
