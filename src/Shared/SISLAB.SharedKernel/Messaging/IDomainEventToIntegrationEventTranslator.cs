using SISLAB.SharedKernel.Domain;

namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Traduz um <see cref="IDomainEvent"/> (interno, rico) em um <see cref="IIntegrationEvent"/>
/// (público, achatado) antes de gravar no Outbox.
///
/// RESPONSABILIDADE:
/// Cada DomainEvent que precisa cruzar bounded contexts deve ter um translator concreto
/// registrado no DI. O <see cref="IDomainEventDispatcher"/> consulta este translator
/// antes de enfileirar a mensagem no Outbox.
///
/// CONVENÇÃO DE LOCALIZAÇÃO:
/// Implemente no projeto Application do módulo emissor. O integration event resultado
/// deve ser definido no projeto Contracts do mesmo módulo (acessível a outros módulos).
///
/// EXEMPLO:
/// <code>
/// public sealed class ItemRegisteredTranslator
///     : IDomainEventToIntegrationEventTranslator&lt;ItemRegisteredEvent&gt;
/// {
///     public IIntegrationEvent Translate(ItemRegisteredEvent domainEvent)
///         => new ItemRegisteredIntegrationEvent(domainEvent.ItemId, domainEvent.OccurredOnUtc);
/// }
/// </code>
/// </summary>
/// <typeparam name="TDomainEvent">Tipo do domain event a ser traduzido.</typeparam>
public interface IDomainEventToIntegrationEventTranslator<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>
    /// Traduz o domain event em um integration event pronto para serialização/Outbox.
    /// </summary>
    IIntegrationEvent Translate(TDomainEvent domainEvent);
}
