using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Outbox;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Tests;

public sealed class OutboxWriterTests
{
    [Fact]
    public void Enqueue_stages_serialized_outbox_message()
    {
        using TestOutboxDbContext context = CreateInMemoryContext();
        var writer = new OutboxWriter(context, new FixedClock(new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc)));

        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "TestIntegrationEvent");
        writer.Enqueue(integrationEvent);

        OutboxMessage staged = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity));
        Assert.Equal(integrationEvent.EventId, staged.Id);
        Assert.Contains("eventType", staged.Payload); // json camelCase
    }

    internal static TestOutboxDbContext CreateInMemoryContext()
    {
        DbContextOptions<TestOutboxDbContext> options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestOutboxDbContext(options);
    }
}

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchToOutbox_translates_enqueues_and_clears_events()
    {
        using TestOutboxDbContext context = OutboxWriterTests.CreateInMemoryContext();
        var writer = new OutboxWriter(context, new FixedClock(DateTime.UtcNow));

        ServiceCollection services = new();
        services.AddSingleton<IDomainEventToIntegrationEventTranslator<TestDomainEvent>, TestDomainEventTranslator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(provider, writer, NullLogger<DomainEventDispatcher>.Instance);

        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseTestEvent(new TestDomainEvent(Guid.NewGuid(), DateTime.UtcNow));

        await dispatcher.DispatchToOutboxAsync(new IHasDomainEvents[] { aggregate });

        Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public async Task DispatchTransactional_invokes_registered_transactional_handler()
    {
        using TestOutboxDbContext context = OutboxWriterTests.CreateInMemoryContext();
        var writer = new OutboxWriter(context, new FixedClock(DateTime.UtcNow));
        var handler = new RecordingTransactionalHandler();

        ServiceCollection services = new();
        services.AddSingleton<ITransactionalDomainEventHandler<TestDomainEvent>>(handler);
        using ServiceProvider provider = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(provider, writer, NullLogger<DomainEventDispatcher>.Instance);

        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseTestEvent(new TestDomainEvent(Guid.NewGuid(), DateTime.UtcNow));

        await dispatcher.DispatchTransactionalAsync(new IHasDomainEvents[] { aggregate });

        Assert.Equal(1, handler.HandleCallCount);
    }
}
