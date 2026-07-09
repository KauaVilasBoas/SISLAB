using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Reads pending Outbox messages and publishes them via <see cref="IEventBus"/>.
/// Invoked by the background worker in SISLAB.Jobs on a configurable interval.
///
/// Idempotency: checks <see cref="OutboxMessage.ProcessedAtUtc"/> == null before publishing.
/// After publishing, marks <see cref="OutboxMessage.MarkProcessed"/> and saves.
/// On failure, records the error via <see cref="OutboxMessage.RecordError"/> and continues
/// with remaining messages — the failed message is retried on the next run.
///
/// The host worker (SISLAB.Jobs) is responsible for creating the DI scope and invoking
/// <see cref="ProcessPendingAsync"/>. This component is stateless and thread-safe.
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

    /// <param name="batchSize">Maximum messages processed per invocation.</param>
    /// <returns>Number of messages successfully published in this run.</returns>
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
                        "Outbox: type '{EventType}' could not be deserialized. MessageId={MessageId}",
                        message.EventType, message.Id);

                    message.RecordError($"Type not found: {message.EventType}");
                    continue;
                }

                // Publish via EventBus using reflection to preserve the concrete type.
                await PublishDynamicAsync(integrationEvent, cancellationToken);

                message.MarkProcessed(_clock.UtcNow);
                published++;

                _logger.LogDebug(
                    "Outbox: published {EventType} | MessageId={MessageId}",
                    message.EventType, message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Outbox: failed to publish {EventType} | MessageId={MessageId}",
                    message.EventType, message.Id);

                message.RecordError(ex.Message);
            }
        }

        // Persist ProcessedAtUtc / Error marks in the same context scope.
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
        // Resolve PublishAsync<TEvent> with the concrete event type via reflection.
        // Reflection cost here is irrelevant — this is a background batch job.
        var publishMethod = typeof(IEventBus)
            .GetMethod(nameof(IEventBus.PublishAsync))!
            .MakeGenericMethod(integrationEvent.GetType());

        await (Task)publishMethod.Invoke(_eventBus, [integrationEvent, cancellationToken])!;
    }
}
