using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Notifications.Contracts;
using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.Modules.Notifications.Infrastructure.Messaging;
using SISLAB.Modules.Notifications.Infrastructure.Persistence;
using SISLAB.Modules.Notifications.Infrastructure.Repositories;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Notifications.Infrastructure.DependencyInjection;

/// <summary>
/// DI composition for the Notifications module (card #64a, Option A).
///
/// Registration order:
/// 1. EF DbContext (Notification aggregate, schema "notifications").
/// 2. Domain repository + idempotent write store (Dapper) + the public publisher port.
/// 3. Write-side unit of work: a no-op domain-event dispatcher (no Outbox under Option A) and the EF UoW,
///    so the mediator's TransactionBehavior can commit the MarkNotificationAsRead command.
/// 4. Notifications schema migrations hosted service.
/// </summary>
public static class NotificationsModuleServiceExtensions
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' is not configured. " +
                "Set it in appsettings.json or User Secrets.");

        // 1. EF DbContext for the module (schema "notifications"). Tenant services (ITenantContext /
        //    ITenantBypass) are resolved into the constructor, so the base applies the company_id query
        //    filter and the tenant-stamping interceptor at runtime.
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "notifications");
                npgsql.MigrationsAssembly(
                    typeof(NotificationsModuleServiceExtensions).Assembly.GetName().Name);
            }));

        // 2. Domain repository (read/update path for MarkAsRead), the idempotent write store, and the public
        //    port other parts of the system raise notifications through. The publisher writes via the store
        //    (Dapper, ON CONFLICT), independent of the mediator transaction, so background jobs can call it.
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();

        // 3. Write-side unit of work for this module's commands. Under Option A there is no Outbox, so the
        //    dispatcher is a no-op that just drains the aggregate's domain events; IUnitOfWork is the shared
        //    EfUnitOfWork bound to THIS module's DbContext. TransactionBehavior (registered per module, like
        //    Inventory) calls SaveChangesAsync after each command.
        services.AddScoped<IDomainEventDispatcher, NoOpDomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<NotificationsDbContext>>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // 4. Applies schema "notifications" migrations at startup (mirrors Identity/Inventory).
        services.AddHostedService<NotificationsSchemaMigrationsHostedService>();

        return services;
    }
}
