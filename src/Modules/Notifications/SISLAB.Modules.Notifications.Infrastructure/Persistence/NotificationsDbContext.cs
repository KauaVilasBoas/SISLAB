using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.Modules.Notifications.Infrastructure.Persistence.Configurations;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// DbContext for the Notifications module (write-side). Manages the <see cref="Notification"/> aggregate in
/// the <c>notifications</c> schema.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Notification"/> is an <see cref="ITenantEntity"/>, so this context is built with the tenant
/// services (<see cref="ITenantContext"/> / <see cref="ITenantBypass"/>) resolved from DI; the base
/// (<see cref="SislabDbContextBase"/>) then applies the global query filter by <c>company_id</c> and installs
/// the tenant-stamping save interceptor. At design time (migrations) the tenant services are absent and the
/// filter is skipped — correct for schema generation.
/// </para>
/// <para>
/// <b>No Outbox.</b> Unlike the Inventory context, this one does NOT implement <c>IOutboxDbContext</c>: under
/// Option A a notification is the terminal effect of an alert, not an event to propagate onward, so nothing is
/// written to an outbox from here. The module registers a no-op domain-event dispatcher so the write-side unit
/// of work still runs (for the <c>MarkNotificationAsRead</c> command) without any outbox machinery.
/// </para>
/// </remarks>
public sealed class NotificationsDbContext : SislabDbContextBase
{
    public NotificationsDbContext(
        DbContextOptions<NotificationsDbContext> options,
        ITenantContext? tenantContext = null,
        ITenantBypass? tenantBypass = null)
        : base(options, tenantContext, tenantBypass) { }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every table of this module lives in the "notifications" schema.
        modelBuilder.HasDefaultSchema("notifications");

        modelBuilder.ApplyConfiguration(new NotificationConfiguration());

        // snake_case naming + tenant query filter applied by the base AFTER the configuration.
        base.OnModelCreating(modelBuilder);
    }
}
