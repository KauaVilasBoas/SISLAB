using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SISLAB.Infrastructure.Modules;

/// <summary>
/// Contrato público de um módulo do monólito modular.
/// Cada bounded context implementa este contrato no seu projeto Application
/// (ou Infrastructure do módulo), expondo-se ao Host sem revelar o Domain interno.
///
/// O Host descobre módulos por assembly scanning e invoca RegisterServices/MapEndpoints
/// em ordem determinística definida pela propriedade <see cref="Order"/>.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Ordem de carregamento. Módulos com menor número são registrados primeiro.
    /// Convenção: Shared=0, Identity=10, Inventory=20.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Registra os serviços do módulo no contêiner de DI.
    /// Chamado durante o bootstrap, antes de WebApplication.Build().
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Mapeia os endpoints mínimos (Minimal API) do módulo no roteador da aplicação.
    /// Chamado após WebApplication.Build(), durante a configuração do pipeline HTTP.
    /// </summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
