using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.Modules.Notifications.Domain.Notifications.Events;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Notifications.Tests.Domain;

public sealed class NotificationTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 9, 30, 0, DateTimeKind.Utc);
    private static readonly FixedClock Clock = new(Now);

    private static Notification NewNotification() => Notification.Raise(
        NotificationType.Expiry,
        NotificationSeverity.Warning,
        "  Reagente vencendo  ",
        "  MTT (lote L-42) vence em 5 dias  ",
        NotificationReference.To("stock_item", Guid.NewGuid()),
        DedupeKey.FromValue("expiry:stock_item:x:2026-07"),
        Clock);

    [Fact]
    public void Raise_captures_the_attributes_trims_text_and_starts_unread()
    {
        Guid target = Guid.NewGuid();

        Notification notification = Notification.Raise(
            NotificationType.LowStock,
            NotificationSeverity.Critical,
            "  Estoque baixo  ",
            "  Etanol abaixo do mínimo  ",
            NotificationReference.To("stock_item", target),
            DedupeKey.FromValue("lowstock:stock_item:x:cycle-1"),
            Clock);

        Assert.Equal(NotificationType.LowStock, notification.Type);
        Assert.Equal(NotificationSeverity.Critical, notification.Severity);
        Assert.Equal("Estoque baixo", notification.Title);
        Assert.Equal("Etanol abaixo do mínimo", notification.Description);
        Assert.Equal("stock_item", notification.Reference.TargetType);
        Assert.Equal(target, notification.Reference.TargetId);
        Assert.Equal("lowstock:stock_item:x:cycle-1", notification.DedupeKey.Value);
        Assert.False(notification.IsRead);
        Assert.Equal(Now, notification.CreatedAtUtc);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public void Raise_emits_NotificationRaised()
    {
        Notification notification = NewNotification();

        NotificationRaisedEvent raised =
            Assert.IsType<NotificationRaisedEvent>(Assert.Single(notification.DomainEvents));
        Assert.Equal(notification.Id, raised.NotificationId);
        Assert.Equal(NotificationType.Expiry, raised.Type);
        Assert.Equal(NotificationSeverity.Warning, raised.Severity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Raise_rejects_a_blank_title(string title)
        => Assert.Throws<DomainException>(() => Notification.Raise(
            NotificationType.Expiry,
            NotificationSeverity.Info,
            title,
            "description",
            NotificationReference.To("stock_item", Guid.NewGuid()),
            DedupeKey.FromValue("k"),
            Clock));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Raise_rejects_a_blank_description(string description)
        => Assert.Throws<DomainException>(() => Notification.Raise(
            NotificationType.Expiry,
            NotificationSeverity.Info,
            "title",
            description,
            NotificationReference.To("stock_item", Guid.NewGuid()),
            DedupeKey.FromValue("k"),
            Clock));

    [Fact]
    public void Raise_rejects_a_title_over_the_maximum_length()
        => Assert.Throws<DomainException>(() => Notification.Raise(
            NotificationType.Expiry,
            NotificationSeverity.Info,
            new string('a', 201),
            "description",
            NotificationReference.To("stock_item", Guid.NewGuid()),
            DedupeKey.FromValue("k"),
            Clock));

    [Fact]
    public void MarkAsRead_flips_the_flag_stamps_the_instant_and_emits_the_event()
    {
        Notification notification = NewNotification();
        notification.ClearDomainEvents(); // drop the raised event to isolate the read transition

        notification.MarkAsRead(Clock);

        Assert.True(notification.IsRead);
        Assert.Equal(Now, notification.ReadAtUtc);
        NotificationReadEvent read =
            Assert.IsType<NotificationReadEvent>(Assert.Single(notification.DomainEvents));
        Assert.Equal(notification.Id, read.NotificationId);
    }

    [Fact]
    public void MarkAsRead_is_idempotent_and_does_not_move_the_read_instant()
    {
        var laterClock = new FixedClock(Now.AddHours(1));
        Notification notification = NewNotification();

        notification.MarkAsRead(Clock);
        notification.ClearDomainEvents();

        notification.MarkAsRead(laterClock); // second acknowledgement — a no-op

        Assert.True(notification.IsRead);
        Assert.Equal(Now, notification.ReadAtUtc); // unchanged: not moved to the later instant
        Assert.Empty(notification.DomainEvents);   // no second read event
    }
}
