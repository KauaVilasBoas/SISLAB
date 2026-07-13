using System.Text.Json;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.Audit;

/// <summary>
/// Thin collaborator that records an Inventory sensitive operation into the audit trail (card [E9] #57).
///
/// Centralizes the mechanics every sensitive handler repeats — resolving the actor, stamping the UTC
/// timestamp, serializing a compact payload, and appending via <see cref="IAuditWriter"/> — so each
/// handler only declares <i>what</i> happened (action, entity, payload), not <i>how</i> it is persisted.
/// The write is append-only and best-effort-durable on its own connection: it is invoked <b>after</b> the
/// aggregate mutation, so the trail reflects an operation that actually took place.
/// </summary>
internal sealed class InventoryAuditRecorder
{
    private const string StockItemEntityType = "StockItem";
    private const string EquipmentEntityType = "Equipment";

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAuditWriter _auditWriter;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public InventoryAuditRecorder(
        IAuditWriter auditWriter,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _auditWriter = auditWriter;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    /// <summary>Records an operation on a <c>StockItem</c> (controlled-item movements: consumption, disposal, count).</summary>
    public Task RecordStockItemAsync(
        Guid companyId,
        Guid stockItemId,
        string action,
        object payload,
        CancellationToken cancellationToken)
        => WriteAsync(companyId, StockItemEntityType, stockItemId, action, payload, cancellationToken);

    /// <summary>Records an operation on an <c>Equipment</c> (maintenance, calibration).</summary>
    public Task RecordEquipmentAsync(
        Guid companyId,
        Guid equipmentId,
        string action,
        object payload,
        CancellationToken cancellationToken)
        => WriteAsync(companyId, EquipmentEntityType, equipmentId, action, payload, cancellationToken);

    private Task WriteAsync(
        Guid companyId,
        string entityType,
        Guid entityId,
        string action,
        object payload,
        CancellationToken cancellationToken)
    {
        var entry = new AuditEntry(
            Id: Guid.NewGuid(),
            CompanyId: companyId,
            UserId: _actorAccessor.GetCurrentActor(),
            Action: action,
            EntityType: entityType,
            EntityId: entityId,
            Payload: JsonSerializer.Serialize(payload, PayloadOptions),
            OccurredAtUtc: _clock.UtcNow);

        return _auditWriter.WriteAsync(entry, cancellationToken);
    }
}
