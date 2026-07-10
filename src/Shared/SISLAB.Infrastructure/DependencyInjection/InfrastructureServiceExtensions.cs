using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Data;
using SISLAB.Infrastructure.Messaging;
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

        // Auditable tenant-isolation escape hatch for system/background work (Scoped so a
        // bypass opened inside one unit of work never leaks into another). ITenantContext
        // itself is contributed by the Identity module (it owns the tenant source).
        services.AddScoped<ITenantBypass, TenantBypass>();

        return services;
    }
}
