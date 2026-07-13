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
/// Pending = <see cref="OutboxMessage.ProcessedAtUtc"/> is null AND the message has not been
/// dead-lettered (<see cref="OutboxMessage.DeadLetteredAtUtc"/> is null). After publishing, marks
/// <see cref="OutboxMessage.MarkProcessed"/> and saves. On failure, records the error via
/// <see cref="OutboxMessage.RecordError"/> (which increments the attempt count and dead-letters the
/// message once the limit is reached) and continues with the remaining messages — a failed message is
/// retried on the next run until it is dead-lettered, so one poison message never blocks the batch.
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
    /// <param name="maxAttempts">
    /// Failed-delivery attempts a message tolerates before it is dead-lettered and dropped from the
    /// pending set.
    /// </param>
    /// <returns>Number of messages successfully published in this run.</returns>
    public async Task<int> ProcessPendingAsync(
        int batchSize = 50,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default)
    {
        List<OutboxMessage> pending = await _outboxContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.DeadLetteredAtUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return 0;

        int published = 0;
        int failed = 0;
        int deadLettered = 0;

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

                    RecordFailure(message, $"Type not found: {message.EventType}", maxAttempts,
                        ref failed, ref deadLettered);
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

                RecordFailure(message, ex.Message, maxAttempts, ref failed, ref deadLettered);
            }
        }

        // Persist ProcessedAtUtc / AttemptCount / DeadLetteredAtUtc / Error marks in the same context scope.
        if (_outboxContext is DbContext dbContext)
            await dbContext.SaveChangesAsync(cancellationToken);

        int backlog = await CountPendingAsync(cancellationToken);

        _logger.LogInformation(
            "Outbox tick: read {Read}, published {Published}, failed {Failed}, dead-lettered {DeadLettered}, backlog {Backlog}.",
            pending.Count, published, failed, deadLettered, backlog);

        return published;
    }

    /// <summary>
    /// Records a failed delivery on the message (increment + possible dead-letter) and updates the
    /// per-tick counters used for the structured tick log.
    /// </summary>
    private void RecordFailure(
        OutboxMessage message,
        string error,
        int maxAttempts,
        ref int failed,
        ref int deadLettered)
    {
        message.RecordError(error, maxAttempts, _clock);
        failed++;

        if (message.IsDeadLettered)
        {
            deadLettered++;
            _logger.LogWarning(
                "Outbox: message dead-lettered after {AttemptCount} attempts. MessageId={MessageId}, EventType={EventType}",
                message.AttemptCount, message.Id, message.EventType);
        }
    }

    private async Task<int> CountPendingAsync(CancellationToken cancellationToken)
    {
        return await _outboxContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.DeadLetteredAtUtc == null)
            .CountAsync(cancellationToken);
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
