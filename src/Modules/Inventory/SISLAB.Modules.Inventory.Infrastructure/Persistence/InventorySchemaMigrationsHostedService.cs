using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Hosted service that applies <see cref="InventoryDbContext"/> migrations on startup
/// (<c>stock_items</c>, <c>storage_locations</c> and <c>outbox_messages</c> in schema
/// <c>inventory</c>). Mirrors the pattern used by the Identity module — each DbContext applies
/// its own schema at boot.
///
/// Creates its own scope because <see cref="InventoryDbContext"/> is registered as Scoped.
/// </summary>
internal sealed class InventorySchemaMigrationsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventorySchemaMigrationsHostedService> _logger;

    public InventorySchemaMigrationsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<InventorySchemaMigrationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying SISLAB Inventory migrations (schema 'inventory')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        InventoryDbContext dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("SISLAB Inventory migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
