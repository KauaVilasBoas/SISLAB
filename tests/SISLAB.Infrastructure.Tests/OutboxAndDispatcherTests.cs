using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Outbox;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

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

/// <summary>
/// Behavioural tests for <see cref="OutboxDispatcher"/>: success marks processed, failures increment
/// the attempt count and, once the limit is reached, dead-letter the message so it leaves the pending
/// set and is never retried again. Uses the in-memory Outbox context and fake event buses.
/// </summary>
public sealed class OutboxDispatcherTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Successful_publish_marks_processed_and_never_dead_letters()
    {
        using TestOutboxDbContext context = OutboxWriterTests.CreateInMemoryContext();
        var bus = new RecordingEventBus();
        OutboxDispatcher dispatcher = CreateDispatcher(context, bus);
        OutboxMessage message = EnqueueMessage(context);
        await context.SaveChangesAsync();

        int published = await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: 5);

        Assert.Equal(1, published);
        Assert.Single(bus.Published);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Equal(Now, message.ProcessedAtUtc);
        Assert.False(message.IsDeadLettered);
        Assert.Equal(0, message.AttemptCount);
        Assert.Empty(await PendingAsync(context));
    }

    [Fact]
    public async Task Each_failure_increments_attempt_count_without_dead_lettering_before_the_limit()
    {
        using TestOutboxDbContext context = OutboxWriterTests.CreateInMemoryContext();
        OutboxDispatcher dispatcher = CreateDispatcher(context, new ThrowingEventBus());
        OutboxMessage message = EnqueueMessage(context);
        await context.SaveChangesAsync();

        await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: 3);
        Assert.Equal(1, message.AttemptCount);
        Assert.False(message.IsDeadLettered);
        Assert.Single(await PendingAsync(context)); // still pending, will be retried

        await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: 3);
        Assert.Equal(2, message.AttemptCount);
        Assert.False(message.IsDeadLettered);
        Assert.Single(await PendingAsync(context));
    }

    [Fact]
    public async Task Repeated_failure_reaches_max_attempts_and_dead_letters_the_message()
    {
        using TestOutboxDbContext context = OutboxWriterTests.CreateInMemoryContext();
        OutboxDispatcher dispatcher = CreateDispatcher(context, new ThrowingEventBus());
        OutboxMessage message = EnqueueMessage(context);
        await context.SaveChangesAsync();

        const int maxAttempts = 3;
        for (int i = 0; i < maxAttempts; i++)
            await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: maxAttempts);

        Assert.Equal(maxAttempts, message.AttemptCount);
        Assert.True(message.IsDeadLettered);
        Assert.Equal(Now, message.DeadLetteredAtUtc);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Empty(await PendingAsync(context)); // dropped from the pending set
    }

    [Fact]
    public async Task Dead_lettered_message_is_excluded_from_pending_and_never_reprocessed()
    {
        using TestOutboxDbContext context = OutboxWriterTests.CreateInMemoryContext();
        var bus = new ThrowingEventBus();
        OutboxDispatcher dispatcher = CreateDispatcher(context, bus);
        OutboxMessage message = EnqueueMessage(context);
        await context.SaveChangesAsync();

        // Two failures with a limit of 2 → dead-lettered on the second tick.
        await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: 2);
        await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: 2);
        Assert.True(message.IsDeadLettered);
        int callsAfterDeadLetter = bus.PublishCallCount;

        // A further tick must NOT pick it up again — the bus is not called any more.
        int published = await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: 2);

        Assert.Equal(0, published);
        Assert.Equal(callsAfterDeadLetter, bus.PublishCallCount);
        Assert.Equal(2, message.AttemptCount);
    }

    [Fact]
    public async Task Poison_message_does_not_block_a_healthy_one_in_the_same_batch()
    {
        using TestOutboxDbContext context = OutboxWriterTests.CreateInMemoryContext();
        // A bus that fails for one specific event id and succeeds for the rest.
        var poisonId = Guid.NewGuid();
        var bus = new SelectiveEventBus(poisonId);
        OutboxDispatcher dispatcher = CreateDispatcher(context, bus);

        OutboxMessage poison = EnqueueMessage(context, poisonId);
        OutboxMessage healthy = EnqueueMessage(context, Guid.NewGuid());
        await context.SaveChangesAsync();

        int published = await dispatcher.ProcessPendingAsync(batchSize: 50, maxAttempts: 5);

        Assert.Equal(1, published);
        Assert.NotNull(healthy.ProcessedAtUtc);
        Assert.Null(poison.ProcessedAtUtc);
        Assert.Equal(1, poison.AttemptCount);
    }

    private static OutboxDispatcher CreateDispatcher(TestOutboxDbContext context, IEventBus bus)
        => new(context, bus, new FixedClock(Now), NullLogger<OutboxDispatcher>.Instance);

    private static OutboxMessage EnqueueMessage(TestOutboxDbContext context, Guid? id = null)
    {
        var integrationEvent = new TestIntegrationEvent(id ?? Guid.NewGuid(), Now, nameof(TestIntegrationEvent));
        var writer = new OutboxWriter(context, new FixedClock(Now));
        writer.Enqueue(integrationEvent);
        return context.ChangeTracker.Entries<OutboxMessage>()
            .Select(e => e.Entity)
            .Single(m => m.Id == integrationEvent.EventId);
    }

    private static Task<List<OutboxMessage>> PendingAsync(TestOutboxDbContext context)
        => context.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.DeadLetteredAtUtc == null)
            .ToListAsync();
}

/// <summary>Event bus that fails only for a chosen event id; used to prove a poison message is isolated.</summary>
public sealed class SelectiveEventBus : IEventBus
{
    private readonly Guid _failingEventId;

    public SelectiveEventBus(Guid failingEventId) => _failingEventId = failingEventId;

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        if (integrationEvent is IIntegrationEvent e && e.EventId == _failingEventId)
            throw new InvalidOperationException("poison event");

        return Task.CompletedTask;
    }
}
