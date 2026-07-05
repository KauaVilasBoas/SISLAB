namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Handler de integration event. Consome eventos publicados via <see cref="IEventBus"/>,
/// originados do Outbox de outro (ou do mesmo) módulo.
///
/// DIFERENÇA DE IDomainEventHandler:
/// - <see cref="IDomainEventHandler{TEvent}"/>: processa eventos internos do domínio.
/// - <see cref="IIntegrationEventHandler{TEvent}"/>: consome eventos públicos/serializados
///   publicados via barramento (Outbox → IEventBus).
///
/// IDEMPOTÊNCIA:
/// Handlers devem ser idempotentes — o mesmo evento pode ser entregue mais de uma vez
/// (reprocessamento de Outbox após falha). Use o EventId do integration event como
/// chave de deduplicação quando necessário.
/// </summary>
/// <typeparam name="TEvent">Tipo do integration event consumido.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class
{
    /// <summary>
    /// Processa o integration event.
    /// </summary>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}
