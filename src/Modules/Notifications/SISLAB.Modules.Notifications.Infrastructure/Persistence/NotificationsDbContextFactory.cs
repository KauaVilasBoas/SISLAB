using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SISLAB.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="NotificationsDbContext"/>.
/// Lets the EF Core CLI generate migrations without a running host. The tenant services are left null — at
/// design time there is no request scope, so the global query filter is not applied and migrations see the
/// full model (correct for schema generation). Uses a throwaway design-time connection string.
/// </summary>
internal sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<NotificationsDbContext> optionsBuilder =
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=sislab_design;Username=postgres;Password=postgres",
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "notifications");
                        npgsql.MigrationsAssembly(
                            typeof(NotificationsDbContextFactory).Assembly.GetName().Name);
                    });

        return new NotificationsDbContext(optionsBuilder.Options);
    }
}
