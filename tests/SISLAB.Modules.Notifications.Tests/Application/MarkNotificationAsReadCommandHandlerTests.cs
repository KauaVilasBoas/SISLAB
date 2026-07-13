using SISLAB.Modules.Notifications.Application.Notifications;
using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.Modules.Notifications.Tests.TestSupport;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Notifications.Tests.Application;

public sealed class MarkNotificationAsReadCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly FixedClock Clock = new(Now);

    private static Notification NewUnread() => Notification.Raise(
        NotificationType.Calibration,
        NotificationSeverity.Warning,
        "Calibração pendente",
        "Balança BAL-01 vence a calibração em 3 dias",
        NotificationReference.To("equipment", Guid.NewGuid()),
        DedupeKey.FromValue("calibration:equipment:x:2026-07"),
        Clock);

    [Fact]
    public async Task Marks_an_existing_notification_as_read_and_stages_the_update()
    {
        Notification notification = NewUnread();
        var repository = new FakeNotificationRepository(notification);
        var handler = new MarkNotificationAsReadCommandHandler(repository, Clock);

        await handler.HandleAsync(new MarkNotificationAsReadCommand(notification.Id));

        Assert.True(notification.IsRead);
        Assert.Equal(Now, notification.ReadAtUtc);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task Throws_NotFound_when_the_notification_does_not_exist_for_the_active_company()
    {
        var repository = new FakeNotificationRepository();
        var handler = new MarkNotificationAsReadCommandHandler(repository, Clock);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new MarkNotificationAsReadCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Marking_an_already_read_notification_succeeds_and_keeps_the_original_read_instant()
    {
        Notification notification = NewUnread();
        notification.MarkAsRead(Clock); // already read at Now
        var repository = new FakeNotificationRepository(notification);
        var laterHandler = new MarkNotificationAsReadCommandHandler(repository, new FixedClock(Now.AddHours(2)));

        await laterHandler.HandleAsync(new MarkNotificationAsReadCommand(notification.Id));

        Assert.True(notification.IsRead);
        Assert.Equal(Now, notification.ReadAtUtc); // unchanged — the aggregate guards the transition
    }
}
