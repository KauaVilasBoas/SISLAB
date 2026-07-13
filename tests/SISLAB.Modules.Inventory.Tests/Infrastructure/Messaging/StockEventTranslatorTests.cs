using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Modules.Inventory.Contracts.Events;
using SISLAB.Modules.Inventory.Domain.StockItems.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.Modules.Inventory.Infrastructure.Messaging;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Tests.Infrastructure.Messaging;

/// <summary>
/// Covers the DomainEvent → IntegrationEvent translators (card [E3] #26): the direct mapping of each
/// translator and the end-to-end path through the <see cref="DomainEventDispatcher"/> proving the
/// flattened contract is written to the Outbox with the correct payload, in the same transaction.
/// </summary>
public sealed class StockEventTranslatorTests
{
    private static readonly UnitOfMeasure Ml = UnitOfMeasure.Milliliter;
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Item = Guid.NewGuid();

    [Fact]
    public void StockReceivedEventTranslator_flattens_quantity_lot_and_expiry()
    {
        var domainEvent = new StockReceivedEvent(
            Company, Item,
            ReceivedQuantity: Quantity.Of(20m, Ml),
            ResultingQuantity: Quantity.Of(120m, Ml),
            Lot: Lot.FromCode("L-2026-01"),
            Expiry: ExpiryDate.FromYearMonth(2027, 6));

        var integrationEvent = Assert.IsType<StockReceivedIntegrationEvent>(
            new StockReceivedEventTranslator().Translate(domainEvent));

        Assert.NotEqual(Guid.Empty, integrationEvent.EventId);
        Assert.Equal(domainEvent.OccurredOnUtc, integrationEvent.OccurredOnUtc);
        Assert.Equal(Company, integrationEvent.CompanyId);
        Assert.Equal(Item, integrationEvent.StockItemId);
        Assert.Equal(20m, integrationEvent.ReceivedQuantity);
        Assert.Equal(120m, integrationEvent.ResultingQuantity);
        Assert.Equal("mL", integrationEvent.Unit);
        Assert.Equal("L-2026-01", integrationEvent.LotCode);
        Assert.Equal(2027, integrationEvent.ExpiryYear);
        Assert.Equal(6, integrationEvent.ExpiryMonth);
    }

    [Fact]
    public void StockReceivedEventTranslator_carries_occurred_on_and_supplier_for_the_read_model()
    {
        Guid supplier = Guid.NewGuid();
        var occurredOn = new DateOnly(2026, 7, 10);
        var domainEvent = new StockReceivedEvent(
            Company, Item,
            ReceivedQuantity: Quantity.Of(20m, Ml),
            ResultingQuantity: Quantity.Of(120m, Ml),
            Lot: null,
            Expiry: null,
            OccurredOn: occurredOn,
            SupplierPartnerId: supplier);

        var integrationEvent = (StockReceivedIntegrationEvent)new StockReceivedEventTranslator().Translate(domainEvent);

        Assert.Equal(occurredOn, integrationEvent.OccurredOn);
        Assert.Equal(supplier, integrationEvent.SupplierPartnerId);
    }

    [Fact]
    public void StockReceivedEventTranslator_leaves_lot_and_expiry_null_when_absent()
    {
        var domainEvent = new StockReceivedEvent(
            Company, Item, Quantity.Of(5m, Ml), Quantity.Of(5m, Ml), Lot: null, Expiry: null);

        var integrationEvent = (StockReceivedIntegrationEvent)new StockReceivedEventTranslator().Translate(domainEvent);

        Assert.Null(integrationEvent.LotCode);
        Assert.Null(integrationEvent.ExpiryYear);
        Assert.Null(integrationEvent.ExpiryMonth);
    }

    [Fact]
    public void StockConsumedEventTranslator_flattens_the_quantities()
    {
        var domainEvent = new StockConsumedEvent(
            Company, Item, ConsumedQuantity: Quantity.Of(30m, Ml), ResultingQuantity: Quantity.Of(70m, Ml));

        var integrationEvent = Assert.IsType<StockConsumedIntegrationEvent>(
            new StockConsumedEventTranslator().Translate(domainEvent));

        Assert.Equal(Company, integrationEvent.CompanyId);
        Assert.Equal(Item, integrationEvent.StockItemId);
        Assert.Equal(30m, integrationEvent.ConsumedQuantity);
        Assert.Equal(70m, integrationEvent.ResultingQuantity);
        Assert.Equal("mL", integrationEvent.Unit);
    }

    [Fact]
    public void StockConsumedEventTranslator_carries_occurred_on_and_experiment_for_the_read_model()
    {
        Guid experiment = Guid.NewGuid();
        var occurredOn = new DateOnly(2026, 7, 9);
        var domainEvent = new StockConsumedEvent(
            Company, Item,
            ConsumedQuantity: Quantity.Of(30m, Ml),
            ResultingQuantity: Quantity.Of(70m, Ml),
            OccurredOn: occurredOn,
            ExperimentId: experiment);

        var integrationEvent = (StockConsumedIntegrationEvent)new StockConsumedEventTranslator().Translate(domainEvent);

        Assert.Equal(occurredOn, integrationEvent.OccurredOn);
        Assert.Equal(experiment, integrationEvent.ExperimentId);
    }

    [Fact]
    public void StockBelowMinimumEventTranslator_flattens_the_current_and_minimum_quantities()
    {
        var domainEvent = new StockBelowMinimumEvent(
            Company, Item, CurrentQuantity: Quantity.Of(7m, Ml), MinimumQuantity: Quantity.Of(10m, Ml));

        var integrationEvent = Assert.IsType<StockBelowMinimumIntegrationEvent>(
            new StockBelowMinimumEventTranslator().Translate(domainEvent));

        Assert.Equal(Company, integrationEvent.CompanyId);
        Assert.Equal(Item, integrationEvent.StockItemId);
        Assert.Equal(7m, integrationEvent.CurrentQuantity);
        Assert.Equal(10m, integrationEvent.MinimumQuantity);
        Assert.Equal("mL", integrationEvent.Unit);
    }

    [Fact]
    public async Task Dispatcher_writes_the_flattened_StockBelowMinimum_contract_to_the_outbox()
    {
        using InMemoryOutboxDbContext context = CreateInMemoryContext();
        var writer = new OutboxWriter(context, new FixedClock(new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc)));

        ServiceCollection services = new();
        services.AddSingleton<IDomainEventToIntegrationEventTranslator<StockBelowMinimumEvent>, StockBelowMinimumEventTranslator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(provider, writer, NullLogger<DomainEventDispatcher>.Instance);

        var aggregate = new StubAggregate();
        aggregate.Raise(new StockBelowMinimumEvent(Company, Item, Quantity.Of(7m, Ml), Quantity.Of(10m, Ml)));

        await dispatcher.DispatchToOutboxAsync(new IHasDomainEvents[] { aggregate });

        OutboxMessage staged = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity));
        Assert.Contains(nameof(StockBelowMinimumIntegrationEvent), staged.EventType);

        StockBelowMinimumIntegrationEvent payload = Deserialize<StockBelowMinimumIntegrationEvent>(staged.Payload);
        Assert.Equal(staged.Id, payload.EventId);
        Assert.Equal(Company, payload.CompanyId);
        Assert.Equal(Item, payload.StockItemId);
        Assert.Equal(7m, payload.CurrentQuantity);
        Assert.Equal(10m, payload.MinimumQuantity);
        Assert.Equal("mL", payload.Unit);

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public async Task Dispatcher_routes_each_domain_event_to_its_own_translator_by_type()
    {
        // Both movement translators registered — the dispatcher must resolve the translator by the
        // event's runtime type, never by registration order. A received event must yield the received
        // contract and a consumed event the consumed contract, each in its own OutboxMessage.
        using InMemoryOutboxDbContext context = CreateInMemoryContext();
        var writer = new OutboxWriter(context, new FixedClock(new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc)));

        ServiceCollection services = new();
        services.AddSingleton<IDomainEventToIntegrationEventTranslator<StockReceivedEvent>, StockReceivedEventTranslator>();
        services.AddSingleton<IDomainEventToIntegrationEventTranslator<StockConsumedEvent>, StockConsumedEventTranslator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(provider, writer, NullLogger<DomainEventDispatcher>.Instance);

        var aggregate = new StubAggregate();
        aggregate.Raise(new StockReceivedEvent(
            Company, Item, Quantity.Of(20m, Ml), Quantity.Of(120m, Ml), Lot: null, Expiry: null));
        aggregate.Raise(new StockConsumedEvent(
            Company, Item, Quantity.Of(30m, Ml), Quantity.Of(90m, Ml)));

        await dispatcher.DispatchToOutboxAsync(new IHasDomainEvents[] { aggregate });

        List<OutboxMessage> staged = context.ChangeTracker.Entries<OutboxMessage>()
            .Select(e => e.Entity)
            .ToList();
        Assert.Equal(2, staged.Count);

        OutboxMessage received = Assert.Single(
            staged, m => m.EventType.Contains(nameof(StockReceivedIntegrationEvent)));
        StockReceivedIntegrationEvent receivedPayload = Deserialize<StockReceivedIntegrationEvent>(received.Payload);
        Assert.Equal(20m, receivedPayload.ReceivedQuantity);
        Assert.Equal(120m, receivedPayload.ResultingQuantity);

        OutboxMessage consumed = Assert.Single(
            staged, m => m.EventType.Contains(nameof(StockConsumedIntegrationEvent)));
        StockConsumedIntegrationEvent consumedPayload = Deserialize<StockConsumedIntegrationEvent>(consumed.Payload);
        Assert.Equal(30m, consumedPayload.ConsumedQuantity);
        Assert.Equal(90m, consumedPayload.ResultingQuantity);
    }

    [Fact]
    public async Task Dispatcher_keeps_a_domain_event_with_no_translator_off_the_outbox()
    {
        // StockCounted is a module-internal audit signal with no translator registered — the dispatcher
        // must leave it off the Outbox (never crossing the boundary) while still clearing it from the
        // aggregate so it is not re-raised.
        using InMemoryOutboxDbContext context = CreateInMemoryContext();
        var writer = new OutboxWriter(context, new FixedClock(new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc)));

        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider, writer, NullLogger<DomainEventDispatcher>.Instance);

        var aggregate = new StubAggregate();
        aggregate.Raise(new StockCountedEvent(Item, Quantity.Of(100m, Ml), Quantity.Of(98m, Ml), Divergence: -2m));

        await dispatcher.DispatchToOutboxAsync(new IHasDomainEvents[] { aggregate });

        Assert.Empty(context.ChangeTracker.Entries<OutboxMessage>());
        Assert.Empty(aggregate.DomainEvents);
    }

    private static TIntegrationEvent Deserialize<TIntegrationEvent>(string payload) =>
        JsonSerializer.Deserialize<TIntegrationEvent>(
            payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

    private static InMemoryOutboxDbContext CreateInMemoryContext()
    {
        DbContextOptions<InMemoryOutboxDbContext> options = new DbContextOptionsBuilder<InMemoryOutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new InMemoryOutboxDbContext(options);
    }

    private sealed class InMemoryOutboxDbContext : DbContext, IOutboxDbContext
    {
        public InMemoryOutboxDbContext(DbContextOptions<InMemoryOutboxDbContext> options) : base(options) { }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    }

    private sealed class StubAggregate : AggregateRoot<Guid>
    {
        public StubAggregate() : base(Item) { }

        public void Raise(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
