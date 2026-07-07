using SISLAB.SharedKernel.Domain;

namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Despacha domain events coletados nos agregados após (ou antes de) a persistência,
/// de acordo com a estratégia de consistência de cada handler:
///
/// - Handlers <see cref="ITransactionalDomainEventHandler{TEvent}"/>: executados de forma
///   síncrona ANTES do SaveChanges, dentro da mesma transação. Falha = rollback completo.
///
/// - Handlers <see cref="IDomainEventHandler{TEvent}"/> (não-transacionais): traduzidos para
///   IntegrationEvents e gravados no Outbox na mesma transação. Publicação é eventual (pós-commit).
///
/// Chamado pela infraestrutura (EfUnitOfWork) durante SaveChangesAsync.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Despacha os handlers transacionais (in-transaction) de todos os eventos pendentes
    /// nos agregados fornecidos. Deve ser chamado ANTES do SaveChanges.
    /// Não limpa os eventos — aguarda confirmação de SaveChanges para fazê-lo.
    /// </summary>
    Task DispatchTransactionalAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Traduz os domain events dos agregados fornecidos para IntegrationEvents e os grava
    /// no Outbox (via UoW/DbContext), ainda na mesma transação.
    /// Limpa a lista de eventos dos agregados após a gravação no Outbox.
    /// Deve ser chamado APÓS o despacho transacional e ANTES do SaveChanges.
    /// </summary>
    Task DispatchToOutboxAsync(
        IEnumerable<IHasDomainEvents> aggregates,
        CancellationToken cancellationToken = default);
}
