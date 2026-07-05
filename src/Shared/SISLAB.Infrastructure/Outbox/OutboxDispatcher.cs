using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Lê mensagens pendentes do Outbox e as publica via <see cref="IEventBus"/>.
/// Acionado pelo background worker do E6 em intervalo configurável.
///
/// IDEMPOTÊNCIA:
/// Antes de publicar, verifica <see cref="OutboxMessage.ProcessedAtUtc"/> == null.
/// Após publicar, marca <see cref="OutboxMessage.MarkProcessed"/> e salva.
/// Em caso de falha, registra o erro via <see cref="OutboxMessage.RecordError"/> e continua
/// com as demais mensagens — a mensagem com erro será retentada na próxima execução.
///
/// NOTA: O host worker (E6) é responsável por criar o escopo DI e invocar
/// <see cref="ProcessPendingAsync"/>. Este componente é stateless e thread-safe.
/// </summary>
public sealed class OutboxDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IOutboxDbContext _outboxContext;
    private readonly IEventBus _eventBus;
    private readonly IClock _clock;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IOutboxDbContext outboxContext,
        IEventBus eventBus,
        IClock clock,
        ILogger<OutboxDispatcher> logger)
    {
        _outboxContext = outboxContext;
        _eventBus = eventBus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Processa um lote de mensagens pendentes do Outbox.
    /// </summary>
    /// <param name="batchSize">Máximo de mensagens processadas por invocação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Quantidade de mensagens publicadas com sucesso nesta execução.</returns>
    public async Task<int> ProcessPendingAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        List<OutboxMessage> pending = await _outboxContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return 0;

        int published = 0;

        foreach (OutboxMessage message in pending)
        {
            try
            {
                object? integrationEvent = DeserializeEvent(message);

                if (integrationEvent is null)
                {
                    _logger.LogWarning(
                        "Outbox: tipo '{EventType}' não pôde ser desserializado. MessageId={MessageId}",
                        message.EventType, message.Id);

                    message.RecordError($"Tipo não encontrado: {message.EventType}");
                    continue;
                }

                // Publica via EventBus usando reflexão para preservar o tipo concreto.
                await PublishDynamicAsync(integrationEvent, cancellationToken);

                message.MarkProcessed(_clock.UtcNow);
                published++;

                _logger.LogDebug(
                    "Outbox: publicado {EventType} | MessageId={MessageId}",
                    message.EventType, message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Outbox: falha ao publicar {EventType} | MessageId={MessageId}",
                    message.EventType, message.Id);

                message.RecordError(ex.Message);
            }
        }

        // Persiste marcações (ProcessedAtUtc / Error) no mesmo contexto.
        // O DbContext aqui é o mesmo escopo do caller — não abre nova transação.
        if (_outboxContext is DbContext dbContext)
            await dbContext.SaveChangesAsync(cancellationToken);

        return published;
    }

    private static object? DeserializeEvent(OutboxMessage message)
    {
        Type? eventType = Type.GetType(message.EventType);

        if (eventType is null)
            return null;

        return JsonSerializer.Deserialize(message.Payload, eventType, JsonOptions);
    }

    private async Task PublishDynamicAsync(object integrationEvent, CancellationToken cancellationToken)
    {
        // Resolve PublishAsync<TEvent> com o tipo concreto do evento via reflexão.
        // O custo de reflexão aqui é irrelevante dado que é um background job por lote.
        var publishMethod = typeof(IEventBus)
            .GetMethod(nameof(IEventBus.PublishAsync))!
            .MakeGenericMethod(integrationEvent.GetType());

        await (Task)publishMethod.Invoke(_eventBus, [integrationEvent, cancellationToken])!;
    }
}
