using SISLAB.Modules.Notifications.Application.Notifications;
using SISLAB.Modules.Notifications.Domain.Notifications;

namespace SISLAB.Modules.Notifications.Tests.Application;

/// <summary>
/// Unit tests for the "marcar todas como lidas" command handler (card [E7] #65). They pin the behaviour the
/// bell relies on: every unread notification of the active company is acknowledged in one shot, the read instant
/// comes from the injected clock, and the operation is idempotent (a second run, or a run with nothing unread,
/// is a clean no-op reporting zero). The set-based tenant scoping itself lives in the repository/store and is
/// proven end-to-end against PostgreSQL in <c>NotificationDedupeAndReadIntegrationTests</c>.
/// </summary>
public sealed class MarkAllNotificationsAsReadCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly FixedClock Clock = new(Now);

    private static Notification NewUnread(string dedupeSuffix) => Notification.Raise(
        NotificationType.Expiry,
        NotificationSeverity.Warning,
        "Reagente vencendo",
        "MTT vence em 5 dias",
        NotificationReference.To("stock_item", Guid.NewGuid()),
        DedupeKey.FromValue($"expiry:stock_item:{dedupeSuffix}:2026-07"),
        Clock);

    [Fact]
    public async Task Marks_every_unread_notification_as_read_and_returns_the_count()
    {
        Notification first = NewUnread("a");
        Notification second = NewUnread("b");
        var repository = new FakeNotificationRepository(first, second);
        var handler = new MarkAllNotificationsAsReadCommandHandler(repository, Clock);

        int marked = await handler.HandleAsync(new MarkAllNotificationsAsReadCommand());

        Assert.Equal(2, marked);
        Assert.True(first.IsRead);
        Assert.True(second.IsRead);
        Assert.Equal(Now, first.ReadAtUtc);
        Assert.Equal(Now, second.ReadAtUtc);
    }

    [Fact]
    public async Task Is_a_no_op_and_returns_zero_when_there_is_nothing_unread()
    {
        var repository = new FakeNotificationRepository();
        var handler = new MarkAllNotificationsAsReadCommandHandler(repository, Clock);

        int marked = await handler.HandleAsync(new MarkAllNotificationsAsReadCommand());

        Assert.Equal(0, marked);
    }

    [Fact]
    public async Task Idempotent_second_run_marks_nothing_and_keeps_the_original_read_instant()
    {
        Notification alreadyRead = NewUnread("a");
        alreadyRead.MarkAsRead(Clock); // read at Now
        Notification stillUnread = NewUnread("b");
        var repository = new FakeNotificationRepository(alreadyRead, stillUnread);

        // A later run must only touch the still-unread row and never move the already-read instant.
        var laterHandler = new MarkAllNotificationsAsReadCommandHandler(repository, new FixedClock(Now.AddHours(2)));

        int firstRun = await laterHandler.HandleAsync(new MarkAllNotificationsAsReadCommand());
        int secondRun = await laterHandler.HandleAsync(new MarkAllNotificationsAsReadCommand());

        Assert.Equal(1, firstRun);  // only the previously-unread row was flipped
        Assert.Equal(0, secondRun); // nothing left to acknowledge
        Assert.Equal(Now, alreadyRead.ReadAtUtc);          // untouched — original instant preserved
        Assert.Equal(Now.AddHours(2), stillUnread.ReadAtUtc); // flipped at the later run's clock
    }
}
