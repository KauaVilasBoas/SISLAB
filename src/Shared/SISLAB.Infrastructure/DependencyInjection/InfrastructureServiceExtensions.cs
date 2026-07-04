using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Data;
using SISLAB.Infrastructure.Messaging;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Infrastructure.DependencyInjection;

/// <summary>
/// Extensões de DI para registrar os serviços de infraestrutura compartilhada.
/// Cada módulo também terá seu próprio método de extensão análogo.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registra os serviços de infraestrutura base: mediator, clock, DbConnectionFactory.
    /// </summary>
    public static IServiceCollection AddSislabInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<DbConnectionFactory>();

        return services;
    }
}
