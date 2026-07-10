using Microsoft.AspNetCore.Builder;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;

namespace SISLAB.Modules.Identity.Application;

/// <summary>
/// Pipeline extension exposed to the host by the Identity module.
/// The host references only the Application project — not the internal Infrastructure —
/// so this is the public hook for plugging in <see cref="TenantResolutionMiddleware"/>.
/// </summary>
public static class TenantResolutionApplicationBuilderExtensions
{
    /// <summary>
    /// Inserts <see cref="TenantResolutionMiddleware"/> into the pipeline.
    /// MUST be called AFTER <c>UseAuthentication()</c> and BEFORE <c>UseAuthorization()</c>.
    /// </summary>
    public static IApplicationBuilder UseSislabTenantResolution(this IApplicationBuilder app)
        => app.UseTenantResolutionMiddleware();
}
