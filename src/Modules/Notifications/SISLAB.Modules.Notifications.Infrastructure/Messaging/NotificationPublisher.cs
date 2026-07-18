using SISLAB.Modules.Notifications.Contracts;
using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.Modules.Notifications.Infrastructure.Persistence;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Notifications.Infrastructure.Messaging;

/// <summary>
/// Option A implementation of <see cref="INotificationPublisher"/>: translates a
/// <see cref="RaiseNotificationRequest"/> into the <see cref="Notification"/> aggregate (so every domain
/// invariant is enforced) and persists it directly on the module's write side, idempotently by dedupe key.
/// </summary>
/// <remarks>
/// <para>
/// The write goes through <see cref="INotificationStore"/> (Dapper, <c>ON CONFLICT DO NOTHING</c>), not EF's
/// tracked <c>SaveChanges</c>, so a duplicate raise of the same active alert is a clean no-op rather than a
/// unique-index exception. Because it does not depend on the mediator's <c>TransactionBehavior</c>, the port
/// works identically whether it is called from an HTTP request or from a background alert job (#41/#42/#66)
/// resolving it from the shared container.
/// </para>
/// <para>
/// <b>Tenancy.</b> The owning company comes from <see cref="ITenantContext"/> (never the request), matching
/// the write-side rule everywhere else. When no active company is resolved the raise is rejected with a
/// <see cref="ForbiddenException"/> — a notification must belong to a tenant.
/// </para>
/// </remarks>
internal sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationStore _store;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public NotificationPublisher(
        INotificationStore store,
        ITenantContext tenantContext,
        IClock clock)
    {
        _store = store;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<bool> RaiseAsync(
        RaiseNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Guid companyId = _tenantContext.CompanyId;
        if (companyId == Guid.Empty)
            throw new ForbiddenException("A notification cannot be raised without an active company.");

        // Build the aggregate so its invariants (non-blank title/description, valid reference, dedupe key)
        // are enforced before anything touches the database.
        var reference = NotificationReference.To(request.TargetType, request.TargetId);
        var dedupeKey = DedupeKey.FromValue(request.DedupeKey);

        Notification notification = Notification.Raise(
            MapType(request.Type),
            MapSeverity(request.Severity),
            request.Title,
            request.Description,
            reference,
            dedupeKey,
            _clock);

        var row = new NotificationRow(
            notification.Id,
            companyId,
            notification.Type.ToString(),
            notification.Severity.ToString(),
            notification.Title,
            notification.Description,
            notification.Reference.TargetType,
            notification.Reference.TargetId,
            notification.DedupeKey.Value,
            notification.IsRead,
            notification.CreatedAtUtc,
            notification.ReadAtUtc);

        return await _store.TryAppendAsync(row, cancellationToken);
    }

    private static NotificationType MapType(NotificationTypeCode code) => code switch
    {
        NotificationTypeCode.Expiry => NotificationType.Expiry,
        NotificationTypeCode.LowStock => NotificationType.LowStock,
        NotificationTypeCode.Calibration => NotificationType.Calibration,
        NotificationTypeCode.ControlledCompliance => NotificationType.ControlledCompliance,
        NotificationTypeCode.PresentationReminder => NotificationType.PresentationReminder,
        NotificationTypeCode.BioteriumReminder => NotificationType.BioteriumReminder,
        _ => throw new DomainException($"Unknown notification type code '{code}'.")
    };

    private static NotificationSeverity MapSeverity(NotificationSeverityLevel level) => level switch
    {
        NotificationSeverityLevel.Info => NotificationSeverity.Info,
        NotificationSeverityLevel.Warning => NotificationSeverity.Warning,
        NotificationSeverityLevel.Critical => NotificationSeverity.Critical,
        _ => throw new DomainException($"Unknown notification severity level '{level}'.")
    };
}
