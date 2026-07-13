namespace SISLAB.Modules.Notifications.Contracts;

/// <summary>
/// Public command DTO to raise a notification (card #64a, Option A). Producers — the E6 alert jobs
/// (#41/#42/#66) — build this and hand it to <see cref="INotificationPublisher"/>; they never touch the
/// Notifications aggregate or DbContext.
/// </summary>
/// <remarks>
/// <para>
/// This is a plain POCO on the module's public boundary: it uses only BCL primitives and the Contracts-owned
/// enums, so a producer references <c>SISLAB.Modules.Notifications.Contracts</c> alone — never the internal
/// Domain (module-isolation rule, section 2). The target is carried <b>by value</b>
/// (<see cref="TargetType"/> + <see cref="TargetId"/>), so referencing an Inventory item or equipment does
/// not couple the producer to the Inventory domain.
/// </para>
/// <para>
/// <b>Idempotency contract.</b> <see cref="DedupeKey"/> is the natural key of "the same alert, still active,
/// in this cycle". The publisher guarantees that two requests with the same key (for the same company) yield
/// at most one active notification. Producers must bake the cycle/window discriminator into the key
/// (e.g. <c>expiry:stock_item:{id}:2026-07</c>) so the alert can legitimately re-fire in a later cycle.
/// </para>
/// </remarks>
/// <param name="Type">The family of laboratory condition being reported.</param>
/// <param name="Severity">How urgently the operator should act.</param>
/// <param name="Title">Short headline shown in the bell.</param>
/// <param name="Description">One-line detail with the specifics.</param>
/// <param name="TargetType">Kind of the referenced entity (e.g. <c>stock_item</c>, <c>equipment</c>).</param>
/// <param name="TargetId">Identifier of the referenced entity, by value.</param>
/// <param name="DedupeKey">Natural idempotency key of the alert, including its cycle/window bucket.</param>
public sealed record RaiseNotificationRequest(
    NotificationTypeCode Type,
    NotificationSeverityLevel Severity,
    string Title,
    string Description,
    string TargetType,
    Guid TargetId,
    string DedupeKey);
