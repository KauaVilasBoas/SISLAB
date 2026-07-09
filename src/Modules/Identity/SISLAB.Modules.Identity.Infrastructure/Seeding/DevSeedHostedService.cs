using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Hosted service that runs <see cref="LafteDevSeeder"/> on startup, behind the
/// <c>Seed:Enabled</c> flag. Registered AFTER the migration hosted services (SISLAB + Lumen)
/// so all schemas and Lumen's system seed (Administrator/User profiles) already exist when
/// the seed runs (hosted services execute in registration order).
///
/// Seed failures are logged but do NOT crash the application — this is a dev convenience,
/// never a production availability prerequisite.
/// </summary>
internal sealed class DevSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DevSeedOptions _options;
    private readonly ILogger<DevSeedHostedService> _logger;

    public DevSeedHostedService(
        IServiceScopeFactory scopeFactory,
        DevSeedOptions options,
        ILogger<DevSeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            LafteDevSeeder seeder = scope.ServiceProvider.GetRequiredService<LafteDevSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dev seed (LAFTE) failed. Boot continues.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
