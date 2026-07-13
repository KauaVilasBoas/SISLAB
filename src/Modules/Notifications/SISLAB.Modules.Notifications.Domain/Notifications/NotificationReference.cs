using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Notifications.Domain.Notifications;

/// <summary>
/// What a <see cref="Notification"/> points at: the target kind (e.g. <c>stock_item</c>, <c>equipment</c>)
/// and the target's id, held <b>by value</b> as a <see cref="Guid"/>. The Notifications context never owns a
/// foreign key or navigation into another module — it records the reference as opaque data so the bell can
/// deep-link ("Ver item") without coupling to the Inventory aggregates (module-isolation rule, section 2).
/// </summary>
/// <remarks>
/// <see cref="TargetType"/> is a free-form, lower-cased slug rather than an enum on purpose: the set of
/// referenceable things grows with the modules that raise alerts (stock items, equipment, and later
/// experiments), and this context should not need a new enum value — hence a new deploy — every time. The
/// value object still enforces the shape (non-blank, bounded length) so a malformed reference cannot enter
/// the aggregate.
/// </remarks>
public sealed class NotificationReference : ValueObject
{
    private const int MaxTargetTypeLength = 60;

    private NotificationReference(string targetType, Guid targetId)
    {
        TargetType = targetType;
        TargetId = targetId;
    }

    /// <summary>Kind of the referenced entity, a lower-cased slug (e.g. <c>stock_item</c>, <c>equipment</c>).</summary>
    public string TargetType { get; }

    /// <summary>Identifier of the referenced entity, by value — never a cross-module FK.</summary>
    public Guid TargetId { get; }

    /// <summary>
    /// Builds a reference to a target entity. The target type is normalized to a lower-cased, trimmed slug;
    /// a blank type or an empty id is rejected so a notification always points somewhere meaningful.
    /// </summary>
    public static NotificationReference To(string targetType, Guid targetId)
    {
        Guard.AgainstNullOrWhiteSpace(targetType, nameof(targetType));
        string normalized = targetType.Trim().ToLowerInvariant();
        Guard.AgainstMaxLength(normalized, MaxTargetTypeLength, nameof(targetType));
        Guard.AgainstEmptyGuid(targetId, nameof(targetId));

        return new NotificationReference(normalized, targetId);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetType;
        yield return TargetId;
    }

    public override string ToString() => $"{TargetType}:{TargetId}";
}
