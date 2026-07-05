namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Marcador para integration events — representação pública e achatada de um DomainEvent,
/// publicada para outros bounded contexts via Outbox/barramento de eventos.
///
/// DIFERENÇA DE DOMAIN EVENT:
/// - DomainEvent: interno ao módulo, rico em dados de domínio, descartado após despacho.
/// - IntegrationEvent: público, serializado como JSON no Outbox, transportável via fila/broker.
///
/// CONVENÇÃO:
/// Cada DomainEvent que precisa cruzar bounded contexts deve ter um IntegrationEvent
/// correspondente no projeto Contracts do módulo emissor.
/// A tradução (DomainEvent → IntegrationEvent) ocorre no EventHandler do módulo,
/// antes de gravar no Outbox.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Identificador único do evento (para idempotência no consumidor).</summary>
    Guid EventId { get; }

    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTime OccurredOnUtc { get; }

    /// <summary>Nome do tipo do evento (discriminador para desserialização polimórfica).</summary>
    string EventType { get; }
}
