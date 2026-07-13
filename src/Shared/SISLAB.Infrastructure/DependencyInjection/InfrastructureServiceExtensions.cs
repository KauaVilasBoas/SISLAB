using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Data;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddSislabInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<DbConnectionFactory>();

        // Teach Dapper (the read-side) to bind DateOnly/TimeOnly parameters and columns. Dapper 2.1.x does
        // not map these types natively, so without this every read query that passes a DateOnly parameter
        // (expiry @Today, consumption @From/@To) would throw NotSupportedException before reaching Npgsql.
        DapperDateOnlyTypeHandlers.Register();

        // Auditable tenant-isolation escape hatch for system/background work (Scoped so a
        // bypass opened inside one unit of work never leaks into another). ITenantContext
        // itself is contributed by the Identity module (it owns the tenant source).
        services.AddScoped<ITenantBypass, TenantBypass>();

        // Background tenant-override seam (E6 alert jobs #41/#42/#66). Scoped so a company set inside one
        // job iteration never leaks into the next or into an HTTP request. It is safe to register here
        // (unlike ITenantContext, which Identity owns): the effective ITenantContext — composed by the
        // Identity module as OverridableTenantContext — reads this override and, when unset (every HTTP
        // request), transparently falls back to the request-resolved tenant. Registering only the override
        // here keeps the jobs host free of any ITenantContext registration (composition test).
        services.AddScoped<ITenantContextOverride, TenantContextOverride>();

        // Cross-cutting CQRS pipeline behaviors that carry NO module-specific dependency.
        // Registration order defines pipeline position: the Mediator reverses the resolved
        // sequence, so the FIRST registered behavior becomes the OUTERMOST wrapper.
        //   Logging (outermost — observes everything, including validation failures)
        //     → Validation (short-circuits before the handler on invalid input)
        //       → [TransactionBehavior — registered per write-side module, see AddInventoryModule]
        //         → Handler
        // TransactionBehavior is NOT registered here on purpose: it depends on IUnitOfWork,
        // which is a per-module (per-DbContext) service. Registering it globally would force
        // every module that dispatches through the mediator (e.g. Identity, which only issues
        // queries) to register an otherwise-unused IUnitOfWork just to satisfy the constructor.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
