using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Notifications.Infrastructure.DependencyInjection;

namespace SISLAB.Modules.Notifications.Application;

/// <summary>
/// Notifications module entry point for the Composition Root (card #64a). The host references this assembly
/// for auto-discovery via reflection; it never references the internal Domain project directly.
/// </summary>
/// <remarks>
/// <see cref="Order"/> is 30 — after the business modules (Identity = 10, Inventory = 20). Notifications is a
/// cross-cutting sink that the E6 jobs raise into; registering it after the business modules keeps the "later
/// than the domains that feed it" intent, and does not collide with any existing order.
/// </remarks>
public sealed class NotificationsModule : IModule
{
    /// <inheritdoc />
    public int Order => 30;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // MVC controller of this module (NotificationsController) lives in this assembly, co-located with the
        // CQRS queries/command it dispatches. Registering this assembly as an ApplicationPart makes the actions
        // discoverable by MVC. AddControllers is idempotent across modules.
        services
            .AddControllers()
            .AddApplicationPart(typeof(NotificationsModule).Assembly);

        services.AddHandlersFromAssembly(typeof(NotificationsModule).Assembly);

        // FluentValidation validators for this module's commands, so the ValidationBehavior can resolve
        // IValidator<TCommand> from DI. Registered by scan against the closed IValidator<T> interface.
        RegisterValidators(services, typeof(NotificationsModule).Assembly);

        // Write-side composition: module DbContext (schema "notifications"), the repository/store, the public
        // INotificationPublisher port, the no-op domain-event dispatcher + EF unit of work, and the schema
        // migrations hosted service. Symmetric to how Inventory delegates to AddInventoryModule.
        services.AddNotificationsModule(configuration);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // NotificationsController is an attribute-routed MVC controller, discovered via AddApplicationPart and
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
