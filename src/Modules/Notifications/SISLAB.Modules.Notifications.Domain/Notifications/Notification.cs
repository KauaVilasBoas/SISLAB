using SISLAB.Modules.Notifications.Domain.Notifications.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Notifications.Domain.Notifications;

/// <summary>
/// An alert surfaced to the operators of a company in the notification bell (card #64a): a validity risk, a
/// low-stock crossing, a calibration due date, or a controlled-item compliance action. A notification is the
/// <b>terminal</b> effect of an alert cycle (Option A) — the E6 jobs detect the condition and raise it here;
/// this aggregate then owns its read/unread lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotency.</b> Every notification carries a <see cref="DedupeKey"/> (the natural key of "the same
/// alert, still active, in this cycle"). The invariant "one active notification per dedupe key per company"
/// is enforced at the write boundary (partial unique index over unread rows + <c>ON CONFLICT DO NOTHING</c>),
/// not in memory, because the aggregate has no visibility of its siblings — the same defense-in-depth split
/// the Inventory read models use.
/// </para>
/// <para>
/// <b>Tenancy.</b> The notification belongs to exactly one company (<see cref="ITenantEntity"/>). The
/// <see cref="CompanyId"/> is stamped by the persistence interceptor on the write side; the read side scopes
/// every query with <c>WHERE company_id = @CompanyId</c>. There is no user dimension — a notification is a
/// company-wide signal (decision on card #64a).
/// </para>
/// </remarks>
public sealed class Notification : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 1000;

    // Parameterless constructor for EF Core materialization.
    private Notification() : base(Guid.Empty) { }

    private Notification(
        Guid id,
        NotificationType type,
        NotificationSeverity severity,
        string title,
        string description,
        NotificationReference reference,
        DedupeKey dedupeKey,
        DateTime createdAtUtc)
        : base(id)
    {
        Type = type;
        Severity = severity;
        Title = title;
        Description = description;
        Reference = reference;
        DedupeKey = dedupeKey;
        IsRead = false;
        CreatedAtUtc = createdAtUtc;
        ReadAtUtc = null;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>The family of laboratory condition this notification reports.</summary>
    public NotificationType Type { get; private set; }

    /// <summary>How urgently the operator should act.</summary>
    public NotificationSeverity Severity { get; private set; }

    /// <summary>Short headline shown in the bell (e.g. "Reagente vencendo").</summary>
    public string Title { get; private set; } = default!;

    /// <summary>One-line detail with the specifics (e.g. "MTT (lote L-42) vence em 5 dias").</summary>
    public string Description { get; private set; } = default!;

    /// <summary>What the notification points at, by value — for the "Ver item"/"Ver equipamento" deep link.</summary>
    public NotificationReference Reference { get; private set; } = default!;

    /// <summary>Natural idempotency key of the alert; unique among the company's active notifications.</summary>
    public DedupeKey DedupeKey { get; private set; } = default!;

    /// <summary>Whether the operator has already acknowledged this notification.</summary>
    public bool IsRead { get; private set; }

    /// <summary>When the notification was raised (UTC), from <see cref="IClock"/> — never the database clock.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When it was first marked as read (UTC); <see langword="null"/> while unread.</summary>
    public DateTime? ReadAtUtc { get; private set; }

    /// <summary>
    /// Raises a new, unread notification for the active company. All descriptive text is validated and
    /// trimmed so a malformed alert cannot enter the aggregate; timestamps come from the injected
    /// <see cref="IClock"/> so the moment is deterministic and testable.
    /// </summary>
    public static Notification Raise(
        NotificationType type,
        NotificationSeverity severity,
        string title,
        string description,
        NotificationReference reference,
        DedupeKey dedupeKey,
        IClock clock)
    {
        Guard.AgainstNull(reference, nameof(reference));
        Guard.AgainstNull(dedupeKey, nameof(dedupeKey));
        Guard.AgainstNull(clock, nameof(clock));

        Guard.AgainstNullOrWhiteSpace(title, nameof(title));
        string trimmedTitle = title.Trim();
        Guard.AgainstMaxLength(trimmedTitle, MaxTitleLength, nameof(title));

        Guard.AgainstNullOrWhiteSpace(description, nameof(description));
        string trimmedDescription = description.Trim();
        Guard.AgainstMaxLength(trimmedDescription, MaxDescriptionLength, nameof(description));

        var notification = new Notification(
            Guid.NewGuid(),
            type,
            severity,
            trimmedTitle,
            trimmedDescription,
            reference,
            dedupeKey,
            clock.UtcNow);

        notification.RaiseDomainEvent(
            new NotificationRaisedEvent(notification.Id, notification.Type, notification.Severity));

        return notification;
    }

    /// <summary>
    /// Acknowledges the notification. Idempotent: acknowledging an already-read notification is a no-op and
    /// does not move <see cref="ReadAtUtc"/> or re-raise the read event, so the read moment is captured once.
    /// </summary>
    public void MarkAsRead(IClock clock)
    {
        Guard.AgainstNull(clock, nameof(clock));

        if (IsRead)
            return;

        IsRead = true;
        ReadAtUtc = clock.UtcNow;
        RaiseDomainEvent(new NotificationReadEvent(Id));
    }
}
