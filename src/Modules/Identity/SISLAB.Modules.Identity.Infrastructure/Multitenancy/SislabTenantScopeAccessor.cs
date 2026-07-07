using Lumen.Authorization.Contracts;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Implementação de <see cref="ITenantScopeAccessor"/> (Lumen.Authorization.Contracts)
/// que delega ao <see cref="ITenantContext"/> do SISLAB para obter o escopo de autorização.
///
/// A Lumen usa o escopo retornado aqui para filtrar permissões por tenant:
/// <c>GetPermissionCodesByUserIdAsync(userId, scopeId)</c> retorna apenas as permissões
/// que o usuário possui dentro da empresa ativa.
///
/// Registro: sobrepõe o NoOp registrado pela Lumen via TryAdd — deve ser registrado DEPOIS
/// do wiring da Lumen (AddLumenIdentityCore / AddLumenAuthorization) para garantir
/// que o override aconteça.
///
/// Ciclo de vida: Scoped — acompanha a requisição HTTP, assim como o ITenantContext.
/// </summary>
internal sealed class SislabTenantScopeAccessor : ITenantScopeAccessor
{
    private readonly ITenantContext _tenantContext;

    public SislabTenantScopeAccessor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Retorna o ID da empresa ativa como escopo de autorização da Lumen.
    /// Rotas não autenticadas ou sem tenant ativo retornam null (sem escopo → permissões globais).
    /// </summary>
    public Guid? GetCurrentScopeId()
    {
        try
        {
            Guid companyId = _tenantContext.CompanyId;
            return companyId == Guid.Empty ? null : companyId;
        }
        catch
        {
            // ITenantContext pode lançar fora de rotas autenticadas.
            // Nesse caso, nenhum escopo é aplicado.
            return null;
        }
    }
}
