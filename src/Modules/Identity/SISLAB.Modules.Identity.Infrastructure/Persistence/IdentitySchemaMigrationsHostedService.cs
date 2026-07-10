using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Hosted service that applies <see cref="IdentityDbContext"/> migrations on startup
/// (<c>companies</c> and <c>company_memberships</c> in schema <c>tenancy</c>).
///
/// Mirrors the pattern Lumen uses for its own schema migrations — each DbContext applies
/// its schema at boot. Lumen's tables continue to be migrated by Lumen's own hosted services;
/// this one covers only the SISLAB tenancy bounded context.
///
/// Creates its own scope because <see cref="IdentityDbContext"/> is registered as Scoped.
/// </summary>
internal sealed class IdentitySchemaMigrationsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdentitySchemaMigrationsHostedService> _logger;

    public IdentitySchemaMigrationsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<IdentitySchemaMigrationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying SISLAB Identity migrations (schema 'tenancy')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("SISLAB Identity migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
