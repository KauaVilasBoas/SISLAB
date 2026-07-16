using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.Modules.Notifications.Infrastructure.Persistence;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Notifications.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The single-aggregate commit is owned by the unit of work
/// (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so those paths never save here.
/// </summary>
/// <remarks>
/// <para>
/// There is deliberately no <c>Add</c> here: raising a notification is idempotent by dedupe key and goes
/// through <c>INotificationStore</c> (Dapper, <c>ON CONFLICT DO NOTHING</c>), not the tracked-entity path,
/// because EF's <c>SaveChanges</c> would throw on the unique-index conflict instead of silently skipping.
/// </para>
/// <para>
/// <b>Bulk acknowledge.</b> <see cref="MarkAllAsReadForActiveCompanyAsync"/> also delegates to the Dapper
/// <see cref="INotificationStore"/> (a single set-based UPDATE) rather than loading every unread aggregate to
/// flip a flag — the same seam the write path uses. It scopes to the active company from
/// <see cref="ITenantContext"/> explicitly, because the set-based statement bypasses EF's tracked change
/// tracker (and thus its global query filter), so the tenant guard must be applied by hand.
/// </para>
/// </remarks>
internal sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationsDbContext _dbContext;
    private readonly INotificationStore _store;
    private readonly ITenantContext _tenantContext;

    public NotificationRepository(
        NotificationsDbContext dbContext,
        INotificationStore store,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _store = store;
        _tenantContext = tenantContext;
    }

    public async Task<Notification?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Notifications.FirstOrDefaultAsync(notification => notification.Id == id, ct);

    public Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _dbContext.Notifications.Update(notification);
        return Task.CompletedTask;
    }

    public Task<int> MarkAllAsReadForActiveCompanyAsync(DateTime readAtUtc, CancellationToken ct = default)
        => _store.MarkAllReadAsync(_tenantContext.CompanyId, readAtUtc, ct);
}
