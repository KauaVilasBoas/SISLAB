using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Infrastructure.Tests;

// ----------------------------- Requests / handlers -----------------------------

public sealed record PingCommand(string Value) : ICommand<string>;

public sealed class PingCommandHandler : IRequestHandler<PingCommand, string>
{
    public Task<string> HandleAsync(PingCommand request, CancellationToken cancellationToken = default)
        => Task.FromResult(request.Value);
}

public sealed record PingQuery(string Value) : IQuery<string>;

public sealed class PingQueryHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> HandleAsync(PingQuery request, CancellationToken cancellationToken = default)
        => Task.FromResult(request.Value);
}

/// <summary>Behavior que registra a ordem de execução num log compartilhado.</summary>
public sealed class RecordingBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly List<string> _log;
    private readonly string _name;

    public RecordingBehavior(List<string> log, string name)
    {
        _log = log;
        _name = name;
    }

    public async Task<TResult> HandleAsync(TRequest request, RequestHandlerDelegate<TResult> next, CancellationToken cancellationToken = default)
    {
        _log.Add($"{_name}:before");
        TResult result = await next();
        _log.Add($"{_name}:after");
        return result;
    }
}

public sealed class PingCommandValidator : AbstractValidator<PingCommand>
{
    public PingCommandValidator() => RuleFor(x => x.Value).NotEmpty();
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    /// <summary>SaveChanges count observed at the moment the handler ran (0 = commit happens after).</summary>
    public int SaveChangesCallCountAtHandler { get; private set; }

    public void CaptureSaveChangesCallCountAtHandler() => SaveChangesCallCountAtHandler = SaveChangesCallCount;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }
}

// ----------------------------- Events -----------------------------

public sealed record TestIntegrationEvent(Guid EventId, DateTime OccurredOnUtc, string EventType) : IIntegrationEvent;

public sealed class RecordingIntegrationHandler : IIntegrationEventHandler<TestIntegrationEvent>
{
    public int HandleCallCount { get; private set; }

    public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        HandleCallCount++;
        return Task.CompletedTask;
    }
}

public sealed class ThrowingIntegrationHandler : IIntegrationEventHandler<TestIntegrationEvent>
{
    public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("handler falhou de propósito");
}

public sealed record TestDomainEvent(Guid ItemId, DateTime OccurredOnUtc) : IDomainEvent;

public sealed class TestAggregate : AggregateRoot<Guid>
{
    public TestAggregate(Guid id) : base(id) { }

    public void RaiseTestEvent(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
}

public sealed class TestDomainEventTranslator : IDomainEventToIntegrationEventTranslator<TestDomainEvent>
{
    public IIntegrationEvent Translate(TestDomainEvent domainEvent)
        => new TestIntegrationEvent(Guid.NewGuid(), domainEvent.OccurredOnUtc, nameof(TestIntegrationEvent));
}

public sealed class RecordingTransactionalHandler : ITransactionalDomainEventHandler<TestDomainEvent>
{
    public int HandleCallCount { get; private set; }

    public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        HandleCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>DbContext em memória que participa do Outbox, para testes.</summary>
public sealed class TestOutboxDbContext : DbContext, IOutboxDbContext
{
    public TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : base(options) { }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}

public sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}
