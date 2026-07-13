using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.Modules.Notifications.Infrastructure.Persistence;

namespace SISLAB.Modules.Notifications.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The commit is owned by the unit of work
/// (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves.
/// </summary>
/// <remarks>
/// There is deliberately no <c>Add</c> here: raising a notification is idempotent by dedupe key and goes
/// through <c>INotificationStore</c> (Dapper, <c>ON CONFLICT DO NOTHING</c>), not the tracked-entity path,
/// because EF's <c>SaveChanges</c> would throw on the unique-index conflict instead of silently skipping.
/// </remarks>
internal sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationsDbContext _dbContext;

    public NotificationRepository(NotificationsDbContext dbContext) => _dbContext = dbContext;

    public async Task<Notification?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Notifications.FirstOrDefaultAsync(notification => notification.Id == id, ct);

    public Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _dbContext.Notifications.Update(notification);
        return Task.CompletedTask;
    }
}
