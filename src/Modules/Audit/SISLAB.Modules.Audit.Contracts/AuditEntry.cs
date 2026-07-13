namespace SISLAB.Modules.Audit.Contracts;

/// <summary>
/// An append-only audit record for a sensitive operation (card [E9] #57) — the compliance trail for
/// controlled-substance movements and equipment interventions that replaces the laboratory's paper log.
///
/// It is a flat DTO on the module's public boundary (<c>*.Contracts</c>), so other modules (Inventory)
/// can record an entry through <see cref="IAuditWriter"/> without touching the Audit internals. There is
/// no aggregate: the trail is write-once, never mutated, so a rich model would add no invariants to protect.
/// </summary>
/// <param name="Id">Unique id of this audit record.</param>
/// <param name="CompanyId">Owning tenant. Isolation is enforced on read (<c>WHERE company_id</c>).</param>
/// <param name="UserId">
/// Who performed the operation — the JWT <c>sub</c> claim (a string), or <c>"system"</c> for background jobs.
/// Kept as an opaque value with no cross-module FK to Identity.
/// </param>
/// <param name="Action">Business action performed (e.g. <c>consumption</c>, <c>disposal</c>, <c>maintenance</c>).</param>
/// <param name="EntityType">Type of the affected entity (e.g. <c>StockItem</c>, <c>Equipment</c>).</param>
/// <param name="EntityId">Identifier of the affected entity, held by value.</param>
/// <param name="Payload">A compact JSON summary of the operation (quantities, reason, dates) for the trail.</param>
/// <param name="OccurredAtUtc">When the operation occurred (UTC).</param>
public sealed record AuditEntry(
    Guid Id,
    Guid CompanyId,
    string UserId,
    string Action,
    string EntityType,
    Guid EntityId,
    string Payload,
    DateTime OccurredAtUtc);
