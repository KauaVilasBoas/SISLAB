using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Agenda.Infrastructure.DependencyInjection;

namespace SISLAB.Modules.Agenda.Application;

/// <summary>
/// Agenda module entry point (cards [E10] #67/#69/#70/#71). Registers room-booking, biotério and
/// presentations write-side plus the read-side Dapper query handlers. <see cref="Order"/> = 70 (after
/// Experiments at 60), leaving room for later modules.
/// </summary>
public sealed class AgendaModule : IModule
{
    /// <inheritdoc />
    public int Order => 70;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddControllers()
            .AddApplicationPart(typeof(AgendaModule).Assembly);

        services.AddHandlersFromAssembly(typeof(AgendaModule).Assembly);

        services.AddAgendaModule(configuration);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
