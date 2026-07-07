namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Registro de um integration event pendente de publicação, gravado na mesma transação
/// do command que o originou (padrão Transactional Outbox).
///
/// CICLO DE VIDA:
/// 1. Gravado por <see cref="OutboxWriter"/> durante SaveChangesAsync (pré-commit).
/// 2. Lido pelo <see cref="OutboxDispatcher"/> (background) após o commit.
/// 3. Publicado via <see cref="SISLAB.SharedKernel.Messaging.IEventBus"/>.
/// 4. Marcado como ProcessedAtUtc (idempotência: nunca reprocessar).
///
/// IDEMPOTÊNCIA:
/// O dispatcher verifica <see cref="ProcessedAtUtc"/> antes de publicar.
/// Consumidores também devem ser idempotentes usando <see cref="Id"/> como chave de deduplicação.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Identificador único da mensagem (= EventId do IntegrationEvent).</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome totalmente qualificado do tipo do evento (para desserialização).</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Payload JSON serializado do integration event.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Quando o evento original ocorreu (UTC).</summary>
    public DateTime OccurredOnUtc { get; private set; }

    /// <summary>Quando esta mensagem foi criada/enfileirada (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Quando a mensagem foi publicada com sucesso. Nulo = pendente.
    /// Mensagens processadas NÃO são reprocessadas pelo dispatcher.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>
    /// Mensagem de erro da última tentativa fracassada, se houver.
    /// Preservado para diagnóstico; não bloqueia reprocessamento.
    /// </summary>
    public string? Error { get; private set; }

    // Construtor privado: EF Core usa este padrão para reconstruir entidades do banco.
    private OutboxMessage() { }

    /// <summary>
    /// Cria uma nova mensagem de Outbox com o payload serializado do integration event.
    /// </summary>
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

    /// <summary>
    /// Marca a mensagem como processada com sucesso.
    /// </summary>
    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        Error = null;
    }

    /// <summary>
    /// Registra o erro da tentativa de publicação fracassada.
    /// Não marca como processada — o dispatcher tentará novamente.
    /// </summary>
    public void RecordError(string error)
    {
        Error = error;
    }
}
