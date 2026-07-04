namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Barramento de integration events. Publica eventos para consumidores externos
/// (outros módulos ou sistemas). A implementação concreta decide o mecanismo
/// (Outbox + broker, in-process, etc.).
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publica um integration event de forma assíncrona.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class;
}
