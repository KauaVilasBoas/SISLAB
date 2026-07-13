using SISLAB.Modules.Notifications.Domain.Notifications;

namespace SISLAB.Modules.Notifications.Tests.Application;

/// <summary>
/// In-memory <see cref="INotificationRepository"/> for the command-handler tests. It keeps the aggregate the
/// handler loaded and records whether <see cref="UpdateAsync"/> was called, so a test can assert the handler
/// staged the mutation without a live database or the EF unit of work.
/// </summary>
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
}
