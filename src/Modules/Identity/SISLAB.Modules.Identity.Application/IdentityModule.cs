using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Modules;

namespace SISLAB.Modules.Identity.Application;

/// <summary>
/// Ponto de entrada do módulo Identity no Composition Root.
/// O Host referencia este assembly para descoberta automática via reflection;
/// nunca referencia o projeto Domain diretamente.
///
/// Stub do E0: sem serviços nem endpoints reais — implementação completa no E1.
/// </summary>
public sealed class IdentityModule : IModule
{
    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // E1: registrar DbContext do módulo, repositórios, handlers, ITenantContext.
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // E1: mapear endpoints de autenticação (login, refresh, register).
    }
}
