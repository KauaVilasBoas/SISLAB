using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Infrastructure.ReadModels;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Tests.Infrastructure.ReadModels;

/// <summary>
/// Covers the movements read-model projection (card [E4] #33): each stock integration event is projected
/// into exactly one <c>stock_movements</c> row with the right type and traceability metadata, and — the
/// core acceptance criterion — reprocessing the same event is idempotent (does not duplicate the movement).
/// The idempotency is exercised without a live database via a fake store that reproduces the
/// <c>ON CONFLICT (id) DO NOTHING</c> semantics (keyed by the event id).
/// </summary>
public sealed class StockMovementProjectionHandlerTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Item = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 7, 11, 9, 30, 0, DateTimeKind.Utc);

    private readonly FakeStockMovementStore _store = new();
    private readonly StockMovementProjectionHandler _handler;

    public StockMovementProjectionHandlerTests()
        => _handler = new StockMovementProjectionHandler(_store, new FixedClock(Now));

    [Fact]
    public async Task Projects_a_received_event_into_a_single_entry_movement()
    {
        Guid supplier = Guid.NewGuid();
        var received = new StockReceivedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: Now,
            companyId: Company,
            stockItemId: Item,
            receivedQuantity: 20m,
            resultingQuantity: 120m,
            unit: "mL",
            lotCode: "L-2026-01",
            expiryYear: 2027,
            expiryMonth: 6,
            occurredOn: new DateOnly(2026, 7, 10),
            supplierPartnerId: supplier);

        await _handler.HandleAsync(received);

        StockMovementRow row = Assert.Single(_store.Rows);
        Assert.Equal(received.EventId, row.Id);
        Assert.Equal(Company, row.CompanyId);
        Assert.Equal(Item, row.StockItemId);
        Assert.Equal(nameof(StockMovementType.Received), row.MovementType);
        Assert.Equal(20m, row.QuantityAmount);
        Assert.Equal("mL", row.QuantityUnit);
        Assert.Equal(new DateOnly(2026, 7, 10), row.OccurredOn);
        Assert.Equal(supplier, row.PartnerId);
        Assert.Null(row.ExperimentId);
        Assert.Null(row.PerformedBy);
        Assert.Equal(Now, row.CreatedAtUtc);
    }

    [Fact]
    public async Task Projects_a_consumed_event_into_a_single_consumption_movement()
    {
        Guid experiment = Guid.NewGuid();
        var consumed = new StockConsumedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: Now,
            companyId: Company,
            stockItemId: Item,
            consumedQuantity: 30m,
            resultingQuantity: 70m,
            unit: "mL",
            occurredOn: new DateOnly(2026, 7, 9),
            experimentId: experiment);

        await _handler.HandleAsync(consumed);

        StockMovementRow row = Assert.Single(_store.Rows);
        Assert.Equal(consumed.EventId, row.Id);
        Assert.Equal(Company, row.CompanyId);
        Assert.Equal(nameof(StockMovementType.Consumed), row.MovementType);
        Assert.Equal(30m, row.QuantityAmount);
        Assert.Equal(new DateOnly(2026, 7, 9), row.OccurredOn);
        Assert.Equal(experiment, row.ExperimentId);
        Assert.Null(row.PartnerId);
        Assert.Null(row.PerformedBy);
    }

    [Fact]
    public async Task Projects_a_transferred_event_into_a_single_transfer_movement()
    {
        var transferred = new StockTransferredIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: Now,
            companyId: Company,
            stockItemId: Item,
            fromStorageLocationId: Guid.NewGuid(),
            toStorageLocationId: Guid.NewGuid(),
            movedQuantity: 120m,
            unit: "mL",
            occurredOn: new DateOnly(2026, 7, 8));

        await _handler.HandleAsync(transferred);

        StockMovementRow row = Assert.Single(_store.Rows);
        Assert.Equal(transferred.EventId, row.Id);
        Assert.Equal(Company, row.CompanyId);
        Assert.Equal(Item, row.StockItemId);
        Assert.Equal(nameof(StockMovementType.Transferred), row.MovementType);
        Assert.Equal(120m, row.QuantityAmount);
        Assert.Equal("mL", row.QuantityUnit);
        Assert.Equal(new DateOnly(2026, 7, 8), row.OccurredOn);
        Assert.Null(row.ExperimentId);
        Assert.Null(row.PartnerId);
        Assert.Null(row.PerformedBy);
    }

    [Fact]
    public async Task Projects_a_disposed_event_into_a_single_disposal_movement()
    {
        var disposed = new StockDisposedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: Now,
            companyId: Company,
            stockItemId: Item,
            disposedQuantity: 15m,
            resultingQuantity: 5m,
            unit: "mL",
            occurredOn: new DateOnly(2026, 7, 7));

        await _handler.HandleAsync(disposed);

        StockMovementRow row = Assert.Single(_store.Rows);
        Assert.Equal(disposed.EventId, row.Id);
        Assert.Equal(nameof(StockMovementType.Disposed), row.MovementType);
        Assert.Equal(15m, row.QuantityAmount);
        Assert.Equal(new DateOnly(2026, 7, 7), row.OccurredOn);
        Assert.Null(row.ExperimentId);
        Assert.Null(row.PartnerId);
        Assert.Null(row.PerformedBy);
    }

    [Fact]
    public async Task Falls_back_to_the_emission_instant_when_occurred_on_is_not_informed()
    {
        var consumed = new StockConsumedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: Now,
            companyId: Company,
            stockItemId: Item,
            consumedQuantity: 5m,
            resultingQuantity: 15m,
            unit: "mL",
            occurredOn: null,
            experimentId: null);

        await _handler.HandleAsync(consumed);

        StockMovementRow row = Assert.Single(_store.Rows);
        Assert.Equal(DateOnly.FromDateTime(Now), row.OccurredOn);
    }

    [Fact]
    public async Task Reprocessing_the_same_received_event_does_not_duplicate_the_movement()
    {
        var received = new StockReceivedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: Now,
            companyId: Company,
            stockItemId: Item,
            receivedQuantity: 20m,
            resultingQuantity: 120m,
            unit: "mL",
            lotCode: null,
            expiryYear: null,
            expiryMonth: null,
            occurredOn: new DateOnly(2026, 7, 10),
            supplierPartnerId: null);

        // Same event delivered twice (Outbox redelivery after a failure).
        await _handler.HandleAsync(received);
        await _handler.HandleAsync(received);

        StockMovementRow row = Assert.Single(_store.Rows);
        Assert.Equal(received.EventId, row.Id);
    }

    [Fact]
    public async Task Reprocessing_the_same_consumed_event_does_not_duplicate_the_movement()
    {
        var consumed = new StockConsumedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: Now,
            companyId: Company,
            stockItemId: Item,
            consumedQuantity: 30m,
            resultingQuantity: 70m,
            unit: "mL",
            occurredOn: null,
            experimentId: null);

        await _handler.HandleAsync(consumed);
        await _handler.HandleAsync(consumed);

        Assert.Single(_store.Rows);
    }

    [Fact]
    public async Task Distinct_events_produce_distinct_movements()
    {
        var first = new StockConsumedIntegrationEvent(
            Guid.NewGuid(), Now, Company, Item, 10m, 90m, "mL");
        var second = new StockConsumedIntegrationEvent(
            Guid.NewGuid(), Now, Company, Item, 5m, 85m, "mL");

        await _handler.HandleAsync(first);
        await _handler.HandleAsync(second);

        Assert.Equal(2, _store.Rows.Count);
    }

    /// <summary>
    /// In-memory <see cref="IStockMovementStore"/> reproducing the projection's idempotency contract:
    /// a row whose id already exists is a no-op (mirrors <c>ON CONFLICT (id) DO NOTHING</c>), so the test
    /// asserts the same semantics the SQL guarantees, without a live PostgreSQL.
    /// </summary>
    private sealed class FakeStockMovementStore : IStockMovementStore
    {
        private readonly Dictionary<Guid, StockMovementRow> _byId = new();

        public IReadOnlyCollection<StockMovementRow> Rows => _byId.Values;

        public Task AppendAsync(StockMovementRow row, CancellationToken cancellationToken = default)
        {
            _byId.TryAdd(row.Id, row);
            return Task.CompletedTask;
        }
    }
}
