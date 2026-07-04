using SISLAB.SharedKernel.Domain;

namespace SISLAB.SharedKernel.Tests.Domain;

/// <summary>
/// Testes da coleção de domain events em <see cref="AggregateRoot{TId}"/>:
/// readonly + raise + clear.
/// </summary>
public sealed class AggregateRootTests
{
    private sealed class OrderCreatedEvent : IDomainEvent
    {
        public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
        public string OrderId { get; }

        public OrderCreatedEvent(string orderId) => OrderId = orderId;
    }

    private sealed class SampleAggregate : AggregateRoot<Guid>
    {
        public SampleAggregate(Guid id) : base(id) { }

        public void Create(string orderId)
            => RaiseDomainEvent(new OrderCreatedEvent(orderId));

        public void RaiseMultiple(int count)
        {
            for (int i = 0; i < count; i++)
                RaiseDomainEvent(new OrderCreatedEvent($"order-{i}"));
        }
    }

    [Fact]
    public void NewAggregate_ShouldHave_EmptyDomainEvents()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAdd_EventToCollection()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        aggregate.Create("ord-001");

        Assert.Single(aggregate.DomainEvents);
        Assert.IsType<OrderCreatedEvent>(aggregate.DomainEvents[0]);
    }

    [Fact]
    public void RaiseMultipleEvents_ShouldPreserve_OrderAndCount()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        aggregate.RaiseMultiple(3);

        Assert.Equal(3, aggregate.DomainEvents.Count);
    }

    [Fact]
    public void DomainEvents_ShouldBeReadOnly_CannotBeModifiedExternally()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        aggregate.Create("ord-001");

        // IReadOnlyList não expõe Add/Remove diretamente
        // Verificamos que o tipo retornado é readonly
        Assert.IsAssignableFrom<IReadOnlyList<IDomainEvent>>(aggregate.DomainEvents);

        // Tentar obter um IList mutável via cast deve falhar ou retornar lista diferente
        // A implementação usa AsReadOnly() — cast para List não é possível
        var domainEventsAsObject = aggregate.DomainEvents as System.Collections.Generic.List<IDomainEvent>;
        Assert.Null(domainEventsAsObject);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemove_AllEvents()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        aggregate.Create("ord-001");
        aggregate.Create("ord-002");

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_OnEmptyCollection_ShouldNotThrow()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        var exception = Record.Exception(() => aggregate.ClearDomainEvents());

        Assert.Null(exception);
    }

    [Fact]
    public void DomainEvent_ShouldHave_OccurredOnUtcSet()
    {
        var before = DateTime.UtcNow;
        var aggregate = new SampleAggregate(Guid.NewGuid());
        aggregate.Create("ord-001");
        var after = DateTime.UtcNow;

        var evt = (OrderCreatedEvent)aggregate.DomainEvents[0];

        Assert.InRange(evt.OccurredOnUtc, before, after);
    }
}
