using SISLAB.SharedKernel.Domain;

namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Handler de domain event. Implementações são registradas no DI e
/// descobertas pelo <see cref="IDomainEventDispatcher"/> via assembly scanning.
///
/// ESTRATÉGIA DE CONSISTÊNCIA:
/// - Handlers que implementam <see cref="ITransactionalDomainEventHandler{TEvent}"/>
///   são executados de forma síncrona DENTRO da transação (pré-SaveChanges).
///   Uma falha faz rollback de toda a operação — use APENAS para invariantes de negócio.
/// - Handlers que implementam apenas esta interface são executados pós-SaveChanges,
///   via Outbox, de forma eventual. A operação principal NÃO é afetada por falhas aqui.
///   Use para efeitos colaterais (notificações, sincronização entre módulos, etc.).
///
/// Veja também: <seealso cref="ITransactionalDomainEventHandler{TEvent}"/>
/// </summary>
/// <typeparam name="TEvent">Tipo do domain event que este handler processa.</typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Processa o domain event.
    /// </summary>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface: sinaliza que este handler deve ser executado de forma síncrona
/// DENTRO da transação corrente, antes do SaveChanges.
///
/// QUANDO USAR:
/// Use apenas quando o handler precisa garantir atomicidade com a operação que gerou o evento.
/// Exemplo: checar invariante de estoque que cruza dois agregados na mesma transação.
///
/// CONSEQUÊNCIAS:
/// - Qualquer exceção lançada aqui provoca rollback completo da transação.
/// - O handler PODE modificar o DbContext — suas mudanças são incluídas no mesmo SaveChanges.
/// - Não é adequado para chamadas a sistemas externos (HTTP, fila) — use Outbox para isso.
///
/// QUANDO NÃO USAR:
/// Para notificações, emails, sync com outros módulos ou sistemas externos — use
/// <see cref="IDomainEventHandler{TEvent}"/> (via Outbox).
/// </summary>
/// <typeparam name="TEvent">Tipo do domain event que este handler processa.</typeparam>
public interface ITransactionalDomainEventHandler<in TEvent> : IDomainEventHandler<TEvent>
    where TEvent : IDomainEvent
{
}
