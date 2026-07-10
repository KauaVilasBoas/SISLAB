using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Modules;

namespace SISLAB.Modules.Inventory.Application;

/// <summary>
/// Inventory module entry point for the Composition Root.
/// The host references this assembly for auto-discovery via reflection;
/// it never references the internal Domain project directly.
///
/// Stub from E0 — no real services or endpoints yet. Full implementation in E3/E4.
/// </summary>
public sealed class InventoryModule : IModule
{
    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // E3: register module DbContext, repositories for items/locations/movements.
        // E4: register Dapper query handlers.
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // E3/E4: map endpoints for stock, movements, equipment, partners.
    }
}
