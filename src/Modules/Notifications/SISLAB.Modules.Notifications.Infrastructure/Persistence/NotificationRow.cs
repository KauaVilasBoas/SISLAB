namespace SISLAB.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Flat write row for the idempotent <c>INSERT ... ON CONFLICT DO NOTHING</c> in <see cref="INotificationStore"/>.
/// It is the persistence projection of a <c>Notification</c> aggregate: the publisher builds the aggregate (so
/// every invariant is enforced) and then flattens it into this row for the raw insert. The columns match the
/// <c>notifications</c> table produced by the EF configuration exactly.
/// </summary>
internal sealed record NotificationRow(
    Guid Id,
    Guid CompanyId,
    string Type,
    string Severity,
    string Title,
    string Description,
    string ReferenceTargetType,
    Guid ReferenceTargetId,
    string DedupeKey,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);
