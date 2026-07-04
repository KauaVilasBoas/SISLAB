using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SISLAB.Infrastructure.Modules;

/// <summary>
/// Composition Root: descobre todas as implementações de <see cref="IModule"/>
/// presentes nos assemblies informados, ordena por <see cref="IModule.Order"/>
/// e delega o registro de serviços e endpoints.
///
/// Garante que o Host não precise referenciar os projetos internos dos módulos —
/// basta referenciar o assembly de entrada de cada módulo (que expõe IModule).
/// </summary>
public static class ModuleLoader
{
    private static IReadOnlyList<IModule>? _modules;

    /// <summary>
    /// Descobre módulos nos assemblies fornecidos e registra seus serviços.
    /// Deve ser chamado antes de WebApplication.Build().
    /// </summary>
    public static void RegisterModules(
        IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly> moduleAssemblies)
    {
        _modules = DiscoverModules(moduleAssemblies);

        foreach (IModule module in _modules)
            module.RegisterServices(services, configuration);
    }

    /// <summary>
    /// Mapeia os endpoints de todos os módulos carregados.
    /// Deve ser chamado após WebApplication.Build().
    /// </summary>
    public static void MapModuleEndpoints(IEndpointRouteBuilder endpoints)
    {
        if (_modules is null)
            throw new InvalidOperationException(
                $"Nenhum módulo foi carregado. Certifique-se de chamar {nameof(RegisterModules)} antes de {nameof(MapModuleEndpoints)}.");

        foreach (IModule module in _modules)
            module.MapEndpoints(endpoints);
    }

    private static IReadOnlyList<IModule> DiscoverModules(IEnumerable<Assembly> assemblies)
        => assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IModule).IsAssignableFrom(type)
                           && type is { IsAbstract: false, IsInterface: false })
            .Select(type => (IModule)Activator.CreateInstance(type)!)
            .OrderBy(module => module.Order)
            .ToList()
            .AsReadOnly();
}
