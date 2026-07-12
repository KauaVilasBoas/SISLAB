using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Inventory.Application.PublicApi;
using SISLAB.Modules.Inventory.Contracts;
using SISLAB.Modules.Inventory.Infrastructure.DependencyInjection;

namespace SISLAB.Modules.Inventory.Application;

/// <summary>
/// Inventory module entry point for the Composition Root.
/// The host references this assembly for auto-discovery via reflection;
/// it never references the internal Domain project directly.
/// </summary>
public sealed class InventoryModule : IModule
{
    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // MVC controllers of this module (StockMovements/*Controller) live in this assembly,
        // co-located with the CQRS commands they dispatch. Registering this assembly as an
        // ApplicationPart makes the actions discoverable by MVC (and, later, by Lumen's
        // PermissionDiscoveryScanner). AddControllers is idempotent across modules.
        services
            .AddControllers()
            .AddApplicationPart(typeof(InventoryModule).Assembly);

        services.AddHandlersFromAssembly(typeof(InventoryModule).Assembly);

        // FluentValidation validators for this module's commands, so the ValidationBehavior can
        // resolve IValidator<TCommand> from DI. Registered by scan against the closed
        // IValidator<T> interface — the SISLAB baseline has no FluentValidation.DependencyInjection
        // package, so the wiring is done here explicitly. Scoped mirrors the handler lifetime.
        RegisterValidators(services, typeof(InventoryModule).Assembly);

        // Write-side composition (card [E3] #25): module DbContext (schema "inventory"), the
        // repositories the command handlers depend on (IStockItemRepository /
        // IStorageLocationRepository), the unit of work + Outbox wiring, and the schema migrations
        // hosted service. Symmetric to how IdentityModule delegates to AddIdentityModule.
        services.AddInventoryModule(configuration);

        // Public boundary (card [E5] #35): the adapter that implements IInventoryApi by delegating to
        // this module's read-side queries via the mediator. It is registered here (Application), not in
        // AddInventoryModule (Infrastructure), because the adapter and the queries it delegates to both
        // live in Application, and Infrastructure must not reference Application (keeps the module
        // acyclic). Scoped mirrors the mediator/query-handler lifetime.
        services.AddScoped<IInventoryApi, InventoryApi>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Both the write-side (StockItemsController, Partners/Equipment) and the E4 read-side
        // (StockReadController — Dapper query endpoints for the inventory master-detail #46) are MVC
        // controllers (attribute-routed), discovered via AddApplicationPart and mapped by the host's
        // MapControllers. No per-endpoint wiring is needed here.
    }

    private static void RegisterValidators(IServiceCollection services, Assembly assembly)
    {
        Type openValidatorType = typeof(IValidator<>);

        IEnumerable<(Type Service, Type Implementation)> validators = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openValidatorType)
                .Select(i => (Service: i, Implementation: type)));

        foreach ((Type service, Type implementation) in validators)
            services.AddScoped(service, implementation);
    }
}
