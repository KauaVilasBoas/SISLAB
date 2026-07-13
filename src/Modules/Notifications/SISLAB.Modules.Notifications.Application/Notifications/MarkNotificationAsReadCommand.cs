using FluentValidation;
using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Notifications.Application.Notifications;

/// <summary>
/// Acknowledges a notification of the active company (card #64a — "marcar como lida"), clearing it from the
/// unread badge. Write-side: it loads the aggregate (implicitly tenant-scoped), calls the domain behaviour and
/// lets the unit of work commit. The operation is idempotent — marking an already-read notification succeeds
/// and changes nothing (the aggregate guards the transition).
/// </summary>
public sealed record MarkNotificationAsReadCommand(Guid NotificationId) : ICommand;

internal sealed class MarkNotificationAsReadCommandValidator
    : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadCommandValidator()
        => RuleFor(command => command.NotificationId).NotEmpty();
}

internal sealed class MarkNotificationAsReadCommandHandler
    : ICommandHandler<MarkNotificationAsReadCommand>
{
    private readonly INotificationRepository _notifications;
    private readonly IClock _clock;

    public MarkNotificationAsReadCommandHandler(INotificationRepository notifications, IClock clock)
    {
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<Unit> HandleAsync(
        MarkNotificationAsReadCommand request,
        CancellationToken cancellationToken = default)
    {
        Notification notification =
            await _notifications.FindByIdAsync(request.NotificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        notification.MarkAsRead(_clock);

        await _notifications.UpdateAsync(notification, cancellationToken);

        return Unit.Value;
    }
}
