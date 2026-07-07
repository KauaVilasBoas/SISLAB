using Microsoft.AspNetCore.Builder;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Registro do <see cref="TenantResolutionMiddleware"/> no pipeline.
/// Mantido na Infrastructure porque o middleware é interno ao módulo;
/// o Application expõe o wrapper público consumido pelo Host.
/// </summary>
public static class TenantResolutionMiddlewareExtensions
{
    /// <summary>Insere o middleware de resolução de tenant (company ativa via cookie).</summary>
    public static IApplicationBuilder UseTenantResolutionMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
