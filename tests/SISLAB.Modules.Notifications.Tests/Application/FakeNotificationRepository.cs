using SISLAB.Modules.Notifications.Domain.Notifications;

namespace SISLAB.Modules.Notifications.Tests.Application;

/// <summary>
/// In-memory <see cref="INotificationRepository"/> for the command-handler tests. It keeps the aggregates the
/// handler loaded and records whether <see cref="UpdateAsync"/> was called, so a test can assert the handler
/// staged the mutation without a live database or the EF unit of work.
/// </summary>
/// <remarks>
/// The bulk <see cref="MarkAllAsReadForActiveCompanyAsync"/> models the production semantics faithfully so the
/// handler test is meaningful: it flips only the still-unread aggregates (idempotent — an already-read row keeps
/// its original read instant and is not re-counted) and returns how many it flipped. Tenant scoping is exercised
/// through the seeded aggregates: only those in the "active" company are visible to the fake.
/// </remarks>
internal sealed class FakeNotificationRepository : INotificationRepository
{
    private readonly Dictionary<Guid, Notification> _byId = new();

    public FakeNotificationRepository(params Notification[] seed)
    {
        foreach (Notification notification in seed)
            _byId[notification.Id] = notification;
    }

    public int UpdateCount { get; private set; }

    public Task<Notification?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.GetValueOrDefault(id));

    public Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _byId[notification.Id] = notification;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task<int> MarkAllAsReadForActiveCompanyAsync(DateTime readAtUtc, CancellationToken ct = default)
    {
        var clock = new FixedClock(readAtUtc);

        int flipped = 0;
        foreach (Notification notification in _byId.Values)
        {
            if (notification.IsRead)
                continue;

            notification.MarkAsRead(clock);
            flipped++;
        }

        return Task.FromResult(flipped);
    }
}
