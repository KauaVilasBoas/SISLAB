using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence;

/// <summary>
/// Hosted service that applies <see cref="ExperimentsDbContext"/> migrations on startup (the
/// <c>experiments</c> schema: <c>experiments</c>, <c>experiment_steps</c>, <c>wells</c> and
/// <c>outbox_messages</c>). Mirrors the pattern used by the other modules — each DbContext applies its own
/// schema at boot. Creates its own scope because <see cref="ExperimentsDbContext"/> is registered as Scoped.
/// </summary>
internal sealed class ExperimentsSchemaMigrationsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExperimentsSchemaMigrationsHostedService> _logger;

    public ExperimentsSchemaMigrationsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExperimentsSchemaMigrationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying SISLAB Experiments migrations (schema 'experiments')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        ExperimentsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ExperimentsDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("SISLAB Experiments migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
