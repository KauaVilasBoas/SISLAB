using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence;

/// <summary>
/// Hosted service that applies <see cref="ConfigurationDbContext"/> migrations on startup (the per-tenant
/// configuration tables in schema <c>configuration</c>). Mirrors the pattern used by the Identity, Inventory
/// and Notifications modules — each DbContext applies its own schema at boot.
///
/// Creates its own scope because <see cref="ConfigurationDbContext"/> is registered as Scoped.
/// </summary>
internal sealed class ConfigurationSchemaMigrationsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigurationSchemaMigrationsHostedService> _logger;

    public ConfigurationSchemaMigrationsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ConfigurationSchemaMigrationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying SISLAB Configuration migrations (schema 'configuration')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        ConfigurationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("SISLAB Configuration migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
