namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Record of a pending integration event, written in the same transaction as the originating command
/// (Transactional Outbox pattern).
///
/// Lifecycle:
/// 1. Written by <see cref="OutboxWriter"/> during SaveChangesAsync (pre-commit).
/// 2. Read by <see cref="OutboxDispatcher"/> (background) after commit.
/// 3. Published via <see cref="SISLAB.SharedKernel.Messaging.IEventBus"/>.
/// 4. Marked as ProcessedAtUtc (idempotency: never reprocessed).
///
/// Consumers should also be idempotent, using <see cref="Id"/> as the deduplication key.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Unique message id (= EventId of the IntegrationEvent).</summary>
    public Guid Id { get; private set; }

    /// <summary>Fully qualified type name of the event (for deserialization).</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>JSON-serialized payload of the integration event.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// When the message was successfully published. Null = pending.
    /// Processed messages are NOT reprocessed by the dispatcher.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>
    /// Error from the last failed delivery attempt.
    /// Preserved for diagnostics; does not block retries.
    /// </summary>
    public string? Error { get; private set; }

    // Private constructor for EF Core reconstitution.
    private OutboxMessage() { }

    public static OutboxMessage Create(
        Guid id,
        string eventType,
        string payload,
        DateTime occurredOnUtc,
        DateTime createdAtUtc)
    {
        return new OutboxMessage
        {
            Id = id,
            EventType = eventType,
            Payload = payload,
            OccurredOnUtc = occurredOnUtc,
            CreatedAtUtc = createdAtUtc,
            ProcessedAtUtc = null,
            Error = null
        };
    }

    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        Error = null;
    }

    /// <summary>
    /// Records a failed delivery attempt.
    /// Does not mark as processed — the dispatcher will retry.
    /// </summary>
    public void RecordError(string error)
    {
        Error = error;
    }
}
