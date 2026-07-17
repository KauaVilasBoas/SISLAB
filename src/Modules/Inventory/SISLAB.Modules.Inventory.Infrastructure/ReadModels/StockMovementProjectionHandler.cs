using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Infrastructure.ReadModels;

/// <summary>
/// Projects the Inventory stock movements into the desnormalized <c>inventory.stock_movements</c> read
/// model (card [E4] #33). It is the single projection handler for the module's movement events: it
/// consumes the flattened integration events published from the Outbox (post-commit, eventual) and
/// appends ledger rows via <see cref="IStockMovementStore"/>, carrying the operator-supplied traceability
/// metadata (occurred_on, experiment_id, partner_id) and — since card [E4] #109 — the batch and unit cost
/// each movement was charged against.
/// </summary>
/// <remarks>
/// <para>
/// <b>One row per batch allocation (card #109).</b> A consumption or disposal can be drawn FEFO across
/// several batches, each with its own unit cost. To value the cost report faithfully, this handler appends
/// <b>one costed row per allocation</b> (its <c>stock_batch_id</c> + <c>unit_cost_brl</c>), rather than a
/// single averaged row. An entry and a transfer are always a single row. A consumption/disposal that
/// happened to have no allocation (e.g. a legacy event) still appends one uncosted row so the ledger keeps
/// the movement.
/// </para>
/// <para>
/// <b>Idempotency.</b> Each row's identity is derived deterministically from the event's
/// <see cref="IIntegrationEvent.EventId"/> and the allocation index (<see cref="DeterministicRowId"/>), and
/// <see cref="IStockMovementStore.AppendAsync"/> is idempotent on that key (<c>ON CONFLICT (id) DO
/// NOTHING</c>), so redelivery of the same event never duplicates a movement — the card's idempotency
/// criterion holds even with the fan-out.
/// </para>
/// <para>
/// <b>company_id / performed_by.</b> Every row stores the event's company id (so the Dapper reads keep their
/// mandatory <c>WHERE company_id = @CompanyId</c>); performed_by stays null while the module has no user
/// identity (decision on card [E3] #24), to be backfilled by the audit trail (E9).
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
                StockBatchId: integrationEvent.StockBatchId,
                UnitCostBrl: integrationEvent.UnitCostBrl,
                CreatedAtUtc: _clock.UtcNow),
            cancellationToken);

    public Task HandleAsync(
        StockConsumedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => AppendAllocatedAsync(
            integrationEvent.EventId,
            integrationEvent.CompanyId,
            integrationEvent.StockItemId,
            nameof(StockMovementType.Consumed),
            integrationEvent.Unit,
            ResolveOccurredOn(integrationEvent.OccurredOn, integrationEvent.OccurredOnUtc),
            integrationEvent.ConsumedQuantity,
            integrationEvent.Allocations,
            experimentId: integrationEvent.ExperimentId,
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
                StockBatchId: null,
                UnitCostBrl: null,
                CreatedAtUtc: _clock.UtcNow),
            cancellationToken);

    public Task HandleAsync(
        StockDisposedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => AppendAllocatedAsync(
            integrationEvent.EventId,
            integrationEvent.CompanyId,
            integrationEvent.StockItemId,
            nameof(StockMovementType.Disposed),
            integrationEvent.Unit,
            ResolveOccurredOn(integrationEvent.OccurredOn, integrationEvent.OccurredOnUtc),
            integrationEvent.DisposedQuantity,
            integrationEvent.Allocations,
            experimentId: null,
            cancellationToken);

    /// <summary>
    /// Appends one costed ledger row per FEFO batch allocation of a consumption/disposal (card #109). When
    /// the event carries no allocation it appends a single uncosted row for the whole movement, so the ledger
    /// never loses the movement. Row ids are derived deterministically per slice for idempotency.
    /// </summary>
    private async Task AppendAllocatedAsync(
        Guid eventId,
        Guid companyId,
        Guid stockItemId,
        string movementType,
        string unit,
        DateOnly occurredOn,
        decimal totalQuantity,
        IReadOnlyList<StockBatchAllocationDto> allocations,
        Guid? experimentId,
        CancellationToken cancellationToken)
    {
        if (allocations.Count == 0)
        {
            await _store.AppendAsync(
                new StockMovementRow(
                    Id: eventId,
                    CompanyId: companyId,
                    StockItemId: stockItemId,
                    MovementType: movementType,
                    QuantityAmount: totalQuantity,
                    QuantityUnit: unit,
                    OccurredOn: occurredOn,
                    ExperimentId: experimentId,
                    PartnerId: null,
                    StockBatchId: null,
                    UnitCostBrl: null,
                    CreatedAtUtc: _clock.UtcNow),
                cancellationToken);
            return;
        }

        for (int index = 0; index < allocations.Count; index++)
        {
            StockBatchAllocationDto allocation = allocations[index];

            await _store.AppendAsync(
                new StockMovementRow(
                    Id: DeterministicRowId.ForSlice(eventId, index),
                    CompanyId: companyId,
                    StockItemId: stockItemId,
                    MovementType: movementType,
                    QuantityAmount: allocation.Quantity,
                    QuantityUnit: unit,
                    OccurredOn: occurredOn,
                    ExperimentId: experimentId,
                    PartnerId: null,
                    StockBatchId: allocation.BatchId,
                    UnitCostBrl: allocation.UnitCostBrl,
                    CreatedAtUtc: _clock.UtcNow),
                cancellationToken);
        }
    }

    /// <summary>
    /// The movement date always exists on the read model: it is the operator-supplied business date when
    /// informed, otherwise the event's emission instant (UTC) as a fallback — so the ledger is never
    /// missing a date, matching the card's "movements include ... date" criterion.
    /// </summary>
    private static DateOnly ResolveOccurredOn(DateOnly? occurredOn, DateTime occurredOnUtc)
        => occurredOn ?? DateOnly.FromDateTime(occurredOnUtc);
}
