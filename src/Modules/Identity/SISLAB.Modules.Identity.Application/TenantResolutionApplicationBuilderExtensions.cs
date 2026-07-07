using Microsoft.AspNetCore.Builder;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;

namespace SISLAB.Modules.Identity.Application;

/// <summary>
/// Extensão de pipeline do módulo Identity, exposta ao Host (Composition Root).
/// O Host referencia apenas o Application do módulo — não o Infrastructure interno —
/// então este é o ponto público para plugar o <see cref="TenantResolutionMiddleware"/>.
/// </summary>
public static class TenantResolutionApplicationBuilderExtensions
{
    /// <summary>
    /// Insere o <see cref="TenantResolutionMiddleware"/> no pipeline.
    /// DEVE ser chamado APÓS <c>UseAuthentication()</c>/<c>UseAuthorization()</c> — o middleware
    /// depende do principal (JWT) já resolvido para validar a company ativa contra <c>company_user</c>.
    /// </summary>
    public static IApplicationBuilder UseSislabTenantResolution(this IApplicationBuilder app)
        => app.UseTenantResolutionMiddleware();
}
