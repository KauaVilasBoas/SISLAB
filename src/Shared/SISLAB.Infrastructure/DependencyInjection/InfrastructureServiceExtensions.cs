using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Data;
using SISLAB.Infrastructure.Messaging;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddSislabInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<DbConnectionFactory>();

        return services;
    }
}
