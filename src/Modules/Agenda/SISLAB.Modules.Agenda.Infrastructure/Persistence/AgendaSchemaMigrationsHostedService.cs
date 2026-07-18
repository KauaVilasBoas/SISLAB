using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence;

internal sealed class AgendaSchemaMigrationsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgendaSchemaMigrationsHostedService> _logger;

    public AgendaSchemaMigrationsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgendaSchemaMigrationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying SISLAB Agenda migrations (schema 'agenda')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        AgendaDbContext dbContext = scope.ServiceProvider.GetRequiredService<AgendaDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("SISLAB Agenda migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
