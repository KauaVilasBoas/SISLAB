using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SISLAB.Infrastructure.Outbox;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Messaging;

/// <summary>
/// Implementação de <see cref="IDomainEventDispatcher"/>.
///
/// Fluxo dentro do SaveChangesAsync (orquestrado pelo EfUnitOfWork):
///
/// 1. <see cref="DispatchTransactionalAsync"/>: para cada DomainEvent com pelo menos um
///    <see cref="ITransactionalDomainEventHandler{TEvent}"/> registrado no DI,
///    invoca todos os handlers na mesma transação. Falha = rollback.
///
/// 2. <see cref="DispatchToOutboxAsync"/>: eventos restantes (sem handler transacional, ou
///    que também têm handlers não-transacionais) são traduzidos para IntegrationEvents
///    e gravados no Outbox via <see cref="OutboxWriter"/>. Limpa os eventos dos agregados.
///
/// NOTA SOBRE EVENTOS TRANSACIONAIS:
/// Um DomainEvent pode ter TANTO um handler transacional quanto ser gravado no Outbox.
/// O handler transacional roda first (invariante in-transaction); o Outbox cuida dos
/// efeitos colaterais eventuais.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxWriter _outboxWriter;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IServiceProvider serviceProvider,
        OutboxWriter outboxWriter,
        ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchTransactionalAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
    {
        List<IDomainEvent> allEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        foreach (IDomainEvent domainEvent in allEvents)
        {
            await DispatchTransactionalHandlersForEvent(domainEvent, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DispatchToOutboxAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default)
    {
        List<IHasDomainEvents> aggregateList = aggregates.ToList();
        List<IDomainEvent> allEvents = aggregateList
            .SelectMany(a => a.DomainEvents)
            .ToList();

        foreach (IDomainEvent domainEvent in allEvents)
        {
            await EnqueueEventInOutbox(domainEvent, cancellationToken);
        }

        // Limpa os eventos após enfileirar no Outbox.
        // A partir daqui, os eventos estão seguros na tabela outbox_messages.
        foreach (IHasDomainEvents aggregate in aggregateList)
            aggregate.ClearDomainEvents();
    }

    private async Task DispatchTransactionalHandlersForEvent(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        Type eventType = domainEvent.GetType();
        Type handlerInterface = typeof(ITransactionalDomainEventHandler<>).MakeGenericType(eventType);

        IEnumerable<object?> handlers = _serviceProvider.GetServices(handlerInterface);

        foreach (object? handler in handlers)
        {
            if (handler is null) continue;

            _logger.LogDebug(
                "DomainEventDispatcher: executando handler transacional {Handler} para {Event}",
                handler.GetType().Name, eventType.Name);

            // Invocação via reflexão — custo aceitável para domain events (volume baixo).
            // HandleAsync é declarado na interface BASE IDomainEventHandler<>; GetMethod numa
            // interface derivada não retorna membros herdados, então resolvemos na base.
            Type baseHandlerInterface = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handleMethod = baseHandlerInterface.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
            await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
        }
    }

    private Task EnqueueEventInOutbox(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Verifica se existe um translator registrado para este DomainEvent.
        // O translator converte DomainEvent → IIntegrationEvent antes de gravar no Outbox.
        Type eventType = domainEvent.GetType();
        Type translatorInterface = typeof(IDomainEventToIntegrationEventTranslator<>).MakeGenericType(eventType);

        object? translator = _serviceProvider.GetService(translatorInterface);

        if (translator is null)
        {
            _logger.LogDebug(
                "DomainEventDispatcher: nenhum translator para {EventType} — evento não gravado no Outbox.",
                eventType.Name);

            return Task.CompletedTask;
        }

        // Invoca o translator para obter o integration event.
        var translateMethod = translatorInterface.GetMethod(
            nameof(IDomainEventToIntegrationEventTranslator<IDomainEvent>.Translate))!;

        IIntegrationEvent integrationEvent = (IIntegrationEvent)translateMethod.Invoke(translator, [domainEvent])!;

        _outboxWriter.Enqueue(integrationEvent);

        _logger.LogDebug(
            "DomainEventDispatcher: {EventType} traduzido e enfileirado no Outbox como {IntegrationEventType}",
            eventType.Name, integrationEvent.GetType().Name);

        return Task.CompletedTask;
    }
}
