using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Experiments.Application.Experiments;
using SISLAB.Modules.Experiments.Application.Export;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Application.PublicApi;
using SISLAB.Modules.Experiments.Contracts;
using SISLAB.Modules.Experiments.Infrastructure.DependencyInjection;

namespace SISLAB.Modules.Experiments.Application;

/// <summary>
/// Experiments module entry point for the Composition Root (decision card #68). The host references this
/// assembly for auto-discovery via reflection; it never references the internal Domain project directly.
/// </summary>
/// <remarks>
/// <see cref="Order"/> is 60 — after Configuration (50), Audit (40), Notifications (30) and Inventory (20).
/// Nothing consumed at registration time depends on load order; the value simply places the module's schema
/// migration after the others on a fresh database, and leaves room for the earlier business modules.
/// </remarks>
public sealed class ExperimentsModule : IModule
{
    /// <inheritdoc />
    public int Order => 60;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // MVC controller of this module (ExperimentsController) lives in this assembly, co-located with the
        // CQRS commands/queries it dispatches. Registering this assembly as an ApplicationPart makes its
        // actions discoverable by MVC so Lumen's enforcement filter can gate the [RequirePermission] ones.
        // AddControllers is idempotent across modules.
        services
            .AddControllers()
            .AddApplicationPart(typeof(ExperimentsModule).Assembly);

        services.AddHandlersFromAssembly(typeof(ExperimentsModule).Assembly);

        // FluentValidation validators for this module's commands, so the ValidationBehavior can resolve
        // IValidator<TCommand> from DI. Registered by scan against the closed IValidator<T> interface.
        RegisterValidators(services, typeof(ExperimentsModule).Assembly);

        // Calculation strategies (decision card #68 — Strategy resolved by ExperimentType). Each protocol is
        // registered against IExperimentProtocol; the resolver indexes them by their declared type. Adding an
        // assay type is a new registration here, never an edit to a switch or a handler.
        services.AddScoped<IExperimentProtocol, ViabilityCalculationStrategy>();
        services.AddScoped<IExperimentProtocol, NitricOxideCalculationStrategy>();
        services.AddScoped<IExperimentProtocol, VonFreyUpDownCalculationStrategy>();
        services.AddScoped<IExperimentProtocolResolver, ExperimentProtocolResolver>();

        // Prism CSV export formatters (card #79 — one per assay, resolved by formula code from a registry, the
        // same pattern as the calculation strategies). Adding an assay's export is a new registration here.
        services.AddScoped<IPrismCsvFormatter, ViabilityPrismFormatter>();
        services.AddScoped<IPrismCsvFormatter, NitricOxidePrismFormatter>();
        services.AddScoped<IPrismCsvFormatterResolver, PrismCsvFormatterResolver>();

        // In vivo Prism export formatters (card #31 — group × timepoint). A distinct family because an in vivo
        // export also needs the animal→dose-group mapping from the Project, resolved by formula code from its own
        // registry. Adding a behavioural assay's export is a new registration here.
        services.AddScoped<IInVivoPrismFormatter, VonFreyInVivoPrismFormatter>();
        services.AddScoped<IInVivoPrismFormatterResolver, InVivoPrismFormatterResolver>();

        // Public boundary (card [E10.4] #4): the adapter that implements IExperimentDirectory by resolving
        // experiment titles by id through a tenant-scoped Dapper lookup. Lets the Agenda calendar show an
        // experiment's name without JOINing the experiments schema or touching the Experiments internals.
        services.AddScoped<IExperimentDirectory, ExperimentDirectory>();

        // Current-user resolution for the responsibility model (card [E11]): resolves the caller's Lumen user id
        // from the HTTP principal, so the write handlers can enforce responsibility-based edit authorization and
        // default a new experiment's lead responsible to its creator. AddHttpContextAccessor is idempotent.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();

        // Write-side composition: module DbContext (schema "experiments"), the repository, the unit of work +
        // Outbox wiring, the ExperimentCalculated translator and the schema migrations hosted service.
        services.AddExperimentsModule(configuration);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // ExperimentsController is an attribute-routed MVC controller, discovered via AddApplicationPart and
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
