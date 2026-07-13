using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Hosted service that applies <see cref="NotificationsDbContext"/> migrations on startup (the
/// <c>notifications</c> table in schema <c>notifications</c>). Mirrors the pattern used by the Identity and
/// Inventory modules — each DbContext applies its own schema at boot.
///
/// Creates its own scope because <see cref="NotificationsDbContext"/> is registered as Scoped.
/// </summary>
internal sealed class NotificationsSchemaMigrationsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationsSchemaMigrationsHostedService> _logger;

    public NotificationsSchemaMigrationsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationsSchemaMigrationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying SISLAB Notifications migrations (schema 'notifications')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        NotificationsDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("SISLAB Notifications migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
