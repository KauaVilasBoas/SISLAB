using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Notifications.Application.Notifications;

/// <summary>
/// Acknowledges every unread notification of the active company in one shot (card [E7] #65 — "marcar todas como
/// lidas"), clearing the bell badge. Write-side: it delegates to a single set-based UPDATE over the company's
/// unread rows, so it never loads N aggregates just to flip a flag. The operation is idempotent — with nothing
/// unread it changes nothing and reports zero acknowledged.
/// </summary>
/// <remarks>
/// The scope is exactly the one the bell reads: the active company from <c>ITenantContext</c> (never the
/// request), matching <c>ListNotificationsQuery</c>/<c>CountUnreadNotificationsQuery</c>. There is no user
/// dimension — a notification is a company-wide signal (decision on card #64a), so "all for the active company"
/// is the whole visible inbox. No validator: the command carries no input to guard (the company id is ambient).
/// </remarks>
public sealed record MarkAllNotificationsAsReadCommand : ICommand<int>;

internal sealed class MarkAllNotificationsAsReadCommandHandler
    : ICommandHandler<MarkAllNotificationsAsReadCommand, int>
{
    private readonly INotificationRepository _notifications;
    private readonly IClock _clock;

    public MarkAllNotificationsAsReadCommandHandler(INotificationRepository notifications, IClock clock)
    {
        _notifications = notifications;
        _clock = clock;
    }

    public Task<int> HandleAsync(
        MarkAllNotificationsAsReadCommand request,
        CancellationToken cancellationToken = default)
        => _notifications.MarkAllAsReadForActiveCompanyAsync(_clock.UtcNow, cancellationToken);
}
