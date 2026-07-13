using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Configuration.Application.PublicApi;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Configuration.Infrastructure.DependencyInjection;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// Configuration module entry point for the Composition Root (card [E12] #76). The host references this
/// assembly for auto-discovery via reflection; it never references the internal Domain project directly.
/// </summary>
/// <remarks>
/// <see cref="Order"/> is 50 — after the existing modules (Identity = 10, Inventory = 20, Notifications = 30,
/// Audit = 40). Configuration is a transversal settings source the business modules read from; registering it
/// after them keeps a coherent, collision-free order (nothing consumed at registration time depends on load
/// order, since cross-module reads go through the public <see cref="ILabConfiguration"/> port at runtime).
/// </remarks>
public sealed class ConfigurationModule : IModule
{
    /// <inheritdoc />
    public int Order => 50;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // MVC controllers of this module (one per config domain) live in this assembly, co-located with the
        // CQRS commands/queries they dispatch. Registering this assembly as an ApplicationPart makes the
        // actions discoverable by MVC. AddControllers is idempotent across modules.
        services
            .AddControllers()
            .AddApplicationPart(typeof(ConfigurationModule).Assembly);

        services.AddHandlersFromAssembly(typeof(ConfigurationModule).Assembly);

        // FluentValidation validators for this module's commands, so the ValidationBehavior can resolve
        // IValidator<TCommand> from DI. Registered by scan against the closed IValidator<T> interface.
        RegisterValidators(services, typeof(ConfigurationModule).Assembly);

        // Public boundary adapter: LabConfiguration implements ILabConfiguration, the read-only port other
        // modules (Inventory's write-side + read-side) consume for the tenant's config. It delegates to the
        // module's own read queries via the mediator, keeping the Configuration Domain behind the boundary.
        services.AddScoped<ILabConfiguration, LabConfiguration>();

        // Write-side composition: module DbContext (schema "configuration"), repositories, the no-op
        // domain-event dispatcher + EF unit of work, the tenant defaults provisioner and the schema
        // migrations hosted service. Symmetric to how Inventory/Notifications delegate to their module DI.
        services.AddConfigurationModule(configuration);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // The config controllers are attribute-routed MVC controllers, discovered via AddApplicationPart and
        // mapped by the host's MapControllers. No per-endpoint wiring is needed here.
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
