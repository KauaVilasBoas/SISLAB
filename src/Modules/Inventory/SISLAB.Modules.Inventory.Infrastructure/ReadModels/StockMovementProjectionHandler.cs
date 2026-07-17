using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Infrastructure.ReadModels;

/// <summary>
/// Projects the Inventory stock movements into the desnormalized <c>inventory.stock_movements</c> read
/// model (card [E4] #33). It is the single projection handler for the module's movement events: it
/// consumes the flattened integration events published from the Outbox (post-commit, eventual) and
/// appends one ledger row per movement via <see cref="IStockMovementStore"/>, carrying the
/// operator-supplied traceability metadata the write side does not persist (occurred_on, experiment_id,
/// partner_id).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the Outbox / integration events and not the domain event?</b> Keeping the read model updated is
/// an eventual side effect, not a business invariant, so per the module's hybrid consistency strategy it
/// belongs on the Outbox path (post-commit) rather than inside the write transaction — this decouples the
/// read model from the write model and never lets a projection failure roll back the operation. The
/// <see cref="IEventBus"/> delivers the integration events to this handler after the Outbox is dispatched.
/// </para>
/// <para>
/// <b>Idempotency.</b> The Outbox may deliver the same event more than once (redelivery after a failure).
/// The row identity is the event's <see cref="IIntegrationEvent.EventId"/> and
/// <see cref="IStockMovementStore.AppendAsync"/> is idempotent on that key, so reprocessing the same event
/// never duplicates a movement — satisfying the card's idempotency criterion.
/// </para>
/// <para>
/// <b>company_id.</b> Every projected row stores the event's company id, so the read-side Dapper queries
/// (cards #29–#32) keep their mandatory <c>WHERE company_id = @CompanyId</c> tenant scoping.
/// </para>
/// <para>
/// <b>performed_by (responsável).</b> Left null on purpose: the Inventory module has no user identity
/// (only <c>CompanyId</c> via <c>ITenantContext</c> — decision on card [E3] #24). The audit trail
/// (card #57 / E9), which owns the operator identity, can backfill it later.
/// </para>
/// </remarks>
internal sealed class StockMovementProjectionHandler :
    IIntegrationEventHandler<StockReceivedIntegrationEvent>,
    IIntegrationEventHandler<StockConsumedIntegrationEvent>,
    IIntegrationEventHandler<StockTransferredIntegrationEvent>,
    IIntegrationEventHandler<StockDisposedIntegrationEvent>
{
    private readonly IStockMovementStore _store;
    private readonly IClock _clock;

    public StockMovementProjectionHandler(IStockMovementStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public Task HandleAsync(
        StockReceivedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => _store.AppendAsync(
            new StockMovementRow(
                Id: integrationEvent.EventId,
                CompanyId: integrationEvent.CompanyId,
                StockItemId: integrationEvent.StockItemId,
                MovementType: nameof(StockMovementType.Received),
                QuantityAmount: integrationEvent.ReceivedQuantity,
                QuantityUnit: integrationEvent.Unit,
                OccurredOn: ResolveOccurredOn(integrationEvent.OccurredOn, integrationEvent.OccurredOnUtc),
                ExperimentId: null,
                PartnerId: integrationEvent.SupplierPartnerId,
                CreatedAtUtc: _clock.UtcNow),
            cancellationToken);

    public Task HandleAsync(
        StockConsumedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => _store.AppendAsync(
            new StockMovementRow(
                Id: integrationEvent.EventId,
                CompanyId: integrationEvent.CompanyId,
                StockItemId: integrationEvent.StockItemId,
                MovementType: nameof(StockMovementType.Consumed),
                QuantityAmount: integrationEvent.ConsumedQuantity,
                QuantityUnit: integrationEvent.Unit,
                OccurredOn: ResolveOccurredOn(integrationEvent.OccurredOn, integrationEvent.OccurredOnUtc),
                ExperimentId: integrationEvent.ExperimentId,
                PartnerId: null,
                CreatedAtUtc: _clock.UtcNow),
            cancellationToken);

    public Task HandleAsync(
        StockTransferredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => _store.AppendAsync(
            new StockMovementRow(
                Id: integrationEvent.EventId,
                CompanyId: integrationEvent.CompanyId,
                StockItemId: integrationEvent.StockItemId,
                MovementType: nameof(StockMovementType.Transferred),
                QuantityAmount: integrationEvent.MovedQuantity,
                QuantityUnit: integrationEvent.Unit,
                OccurredOn: ResolveOccurredOn(integrationEvent.OccurredOn, integrationEvent.OccurredOnUtc),
                ExperimentId: null,
                PartnerId: null,
                CreatedAtUtc: _clock.UtcNow),
            cancellationToken);

    public Task HandleAsync(
        StockDisposedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => _store.AppendAsync(
            new StockMovementRow(
                Id: integrationEvent.EventId,
                CompanyId: integrationEvent.CompanyId,
                StockItemId: integrationEvent.StockItemId,
                MovementType: nameof(StockMovementType.Disposed),
                QuantityAmount: integrationEvent.DisposedQuantity,
                QuantityUnit: integrationEvent.Unit,
                OccurredOn: ResolveOccurredOn(integrationEvent.OccurredOn, integrationEvent.OccurredOnUtc),
                ExperimentId: null,
                PartnerId: null,
                CreatedAtUtc: _clock.UtcNow),
            cancellationToken);

    /// <summary>
    /// The movement date always exists on the read model: it is the operator-supplied business date when
    /// informed, otherwise the event's emission instant (UTC) as a fallback — so the ledger is never
    /// missing a date, matching the card's "movements include ... date" criterion.
    /// </summary>
    private static DateOnly ResolveOccurredOn(DateOnly? occurredOn, DateTime occurredOnUtc)
        => occurredOn ?? DateOnly.FromDateTime(occurredOnUtc);
}
