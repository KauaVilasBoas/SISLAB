using Lumen.Identity.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Identity.Infrastructure.DependencyInjection;

namespace SISLAB.Modules.Identity.Application;

/// <summary>
/// Ponto de entrada do módulo Identity no Composition Root.
/// O Host referencia este assembly para descoberta automática via reflection;
/// nunca referencia o projeto Domain ou Infrastructure internos diretamente.
///
/// Delega o registro de serviços para <see cref="IdentityModuleServiceExtensions"/>
/// (Infrastructure), mantendo o Application como ponto de entrada limpo.
/// </summary>
public sealed class IdentityModule : IModule
{
    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityModule(configuration);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Mapeia endpoints de autenticação da Lumen: login, refresh, register,
        // confirm-email, forgot-password, reset-password, logout, me.
        // Prefixo "/api/auth" segue a convenção do SISLAB.
        endpoints.MapLumenIdentityEndpoints(prefix: "/api/auth");
    }
}
