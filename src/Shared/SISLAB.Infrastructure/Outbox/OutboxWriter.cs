using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Serializa integration events e os grava no Outbox dentro da transação corrente.
/// Injetado no <see cref="SISLAB.Infrastructure.Persistence.EfUnitOfWork{TContext}"/> para
/// preencher o ponto de extensão do E0.
///
/// O DbContext concreto é tipado como <see cref="IOutboxDbContext"/> para que o writer
/// possa acessar o DbSet&lt;OutboxMessage&gt; sem depender do contexto específico do módulo.
/// </summary>
public sealed class OutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IOutboxDbContext _outboxContext;
    private readonly IClock _clock;

    public OutboxWriter(IOutboxDbContext outboxContext, IClock clock)
    {
        _outboxContext = outboxContext;
        _clock = clock;
    }

    /// <summary>
    /// Serializa e enfileira um integration event no Outbox.
    /// Deve ser chamado ANTES do SaveChanges para que a gravação seja parte da mesma transação.
    /// </summary>
    /// <typeparam name="TEvent">Tipo concreto do integration event (IIntegrationEvent).</typeparam>
    public void Enqueue<TEvent>(TEvent integrationEvent)
        where TEvent : class, IIntegrationEvent
    {
        string payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);

        OutboxMessage message = OutboxMessage.Create(
            id: integrationEvent.EventId,
            eventType: integrationEvent.GetType().AssemblyQualifiedName
                       ?? integrationEvent.GetType().FullName
                       ?? integrationEvent.EventType,
            payload: payload,
            occurredOnUtc: integrationEvent.OccurredOnUtc,
            createdAtUtc: _clock.UtcNow);

        _outboxContext.OutboxMessages.Add(message);
    }
}
