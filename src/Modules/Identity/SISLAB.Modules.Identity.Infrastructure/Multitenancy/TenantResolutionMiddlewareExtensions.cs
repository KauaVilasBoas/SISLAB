using Microsoft.AspNetCore.Builder;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Registers <see cref="TenantResolutionMiddleware"/> in the pipeline.
/// Kept in Infrastructure because the middleware is module-internal;
/// the Application project exposes the public wrapper consumed by the host.
/// </summary>
public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolutionMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
