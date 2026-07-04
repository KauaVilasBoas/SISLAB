using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Modules;

namespace SISLAB.Modules.Inventory.Application;

/// <summary>
/// Ponto de entrada do módulo Inventory no Composition Root.
/// O Host referencia este assembly para descoberta automática via reflection;
/// nunca referencia o projeto Domain diretamente.
///
/// Stub do E0: sem serviços nem endpoints reais — implementação completa no E3/E4.
/// </summary>
public sealed class InventoryModule : IModule
{
    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // E3: registrar DbContext do módulo, repositórios de itens/locais/movimentações.
        // E4: registrar query handlers Dapper.
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // E3/E4: mapear endpoints de estoque, movimentações, equipamentos, parceiros.
    }
}
