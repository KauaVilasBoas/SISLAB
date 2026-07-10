using Lumen.Identity.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
