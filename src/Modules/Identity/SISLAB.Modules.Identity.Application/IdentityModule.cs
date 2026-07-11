using Lumen.Identity.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Identity.Infrastructure.DependencyInjection;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;

namespace SISLAB.Modules.Identity.Application;

public sealed class IdentityModule : IModule
{
    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityModule(configuration);

        // MVC controllers of this module (Administration/*Controller) live in this assembly,
        // co-located with the CQRS queries they dispatch. Registering this assembly as an
        // ApplicationPart makes [RequirePermission]-decorated actions discoverable by Lumen's
        // PermissionDiscoveryScanner (which iterates ControllerActionDescriptors only —
        // Minimal API is invisible to it). AddControllers is idempotent across modules.
        services
            .AddControllers()
            .AddApplicationPart(typeof(IdentityModule).Assembly);

        services.AddHandlersFromAssembly(typeof(IdentityModule).Assembly);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Lumen auth endpoints: login, refresh, register, confirm-email,
        // forgot-password, reset-password, logout, me. Prefix follows SISLAB convention.
        endpoints.MapLumenIdentityEndpoints(prefix: "/api/auth");

        // SISLAB endpoints for active company selection/switching (post-login, httpOnly cookie).
        endpoints.MapActiveCompanyEndpoints();
    }
}
