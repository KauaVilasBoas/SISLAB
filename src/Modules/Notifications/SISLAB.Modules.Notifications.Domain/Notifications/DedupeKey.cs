using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Notifications.Domain.Notifications;

/// <summary>
/// The natural idempotency key of a <see cref="Notification"/>: a stable, opaque string that identifies "the
/// same alert, still active, in the same cycle". Two raise requests carrying the same <see cref="DedupeKey"/>
/// must never produce two live notifications — the write path enforces this with a partial unique index over
/// active (unread) rows plus <c>INSERT ... ON CONFLICT DO NOTHING</c> (same principle as the Inventory
/// <c>stock_movements</c> read model).
/// </summary>
/// <remarks>
/// <para>
/// The producer (an E6 job) is responsible for baking the "cycle/window bucket" into the key so the alert can
/// legitimately re-fire in a later cycle. A typical shape is
/// <c>company + type + reference + window-bucket</c>, e.g. <c>expiry:stock_item:{id}:2026-07</c> for a
/// monthly validity sweep — the same expiring item re-notifies next month, but not twice in July.
/// <see cref="For"/> composes exactly that canonical shape so producers do not each reinvent (and diverge on)
/// the format; <see cref="FromValue"/> accepts a pre-built key for producers with a bespoke bucket.
/// </para>
/// <para>
/// The key is deliberately NOT company-scoped by itself in the uniqueness constraint: the database index is
/// <c>(company_id, dedupe_key)</c>, so the same logical key never collides across tenants even if a producer
/// omits the company from the string. <see cref="For"/> still includes it for readability and safety.
/// </para>
/// </remarks>
public sealed class DedupeKey : ValueObject
{
    private const int MaxLength = 200;

    private DedupeKey(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Composes the canonical dedupe key <c>{type}:{targetType}:{targetId}:{companyId}:{windowBucket}</c>.
    /// The <paramref name="windowBucket"/> is the cycle discriminator (e.g. <c>2026-07</c>, <c>window-30</c>)
    /// that lets the same alert legitimately re-fire in a later cycle; pass a stable constant for an alert
    /// that should only ever exist once while active.
    /// </summary>
    public static DedupeKey For(
        Guid companyId,
        NotificationType type,
        NotificationReference reference,
        string windowBucket)
    {
        Guard.AgainstEmptyGuid(companyId, nameof(companyId));
        Guard.AgainstNull(reference, nameof(reference));
        Guard.AgainstNullOrWhiteSpace(windowBucket, nameof(windowBucket));

        string bucket = windowBucket.Trim().ToLowerInvariant();
        string typeSlug = type.ToString().ToLowerInvariant();

        return FromValue($"{typeSlug}:{reference.TargetType}:{reference.TargetId}:{companyId}:{bucket}");
    }

    /// <summary>
    /// Wraps a pre-composed dedupe key, normalizing and validating its shape. Used by the publisher when the
    /// producer supplies its own key on <c>RaiseNotificationRequest</c>, and by <see cref="For"/> internally.
    /// </summary>
    public static DedupeKey FromValue(string value)
    {
        Guard.AgainstNullOrWhiteSpace(value, nameof(value));
        string normalized = value.Trim().ToLowerInvariant();
        Guard.AgainstMaxLength(normalized, MaxLength, nameof(value));

        return new DedupeKey(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
