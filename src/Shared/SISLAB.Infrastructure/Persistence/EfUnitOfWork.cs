using Microsoft.EntityFrameworkCore;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> baseada em EF Core.
/// Genérica para que cada módulo instancie com seu próprio DbContext derivado.
///
/// FLUXO DE SaveChangesAsync (Estratégia Híbrida de Consistência — E2):
/// 1. Coleta todos os agregados rastreados pelo ChangeTracker que têm domain events pendentes.
/// 2. Despacha handlers TRANSACIONAIS (ITransactionalDomainEventHandler) de forma síncrona.
///    → Falha aqui provoca rollback de toda a transação (invariante de negócio).
/// 3. Enfileira os integration events no Outbox (na mesma transação) via IDomainEventDispatcher.
///    → Falha aqui também provoca rollback (o Outbox é parte da consistência local).
/// 4. Chama SaveChangesAsync no DbContext — persiste tudo atomicamente.
///
/// IMPORTANTE: o despacho EVENTUAL (efeitos colaterais via broker/Outbox) ocorre fora desta
/// classe, no background worker do E6, que lê outbox_messages e publica via IEventBus.
/// </summary>
/// <typeparam name="TContext">DbContext derivado do módulo.</typeparam>
public sealed class EfUnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    private readonly TContext _dbContext;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public EfUnitOfWork(TContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    {
        _dbContext = dbContext;
        _domainEventDispatcher = domainEventDispatcher;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Coleta agregados rastreados que possuem domain events pendentes.
        List<IHasDomainEvents> aggregatesWithEvents = _dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        // Passo 1: despacha handlers transacionais (in-transaction, com rollback em falha).
        await _domainEventDispatcher.DispatchTransactionalAsync(aggregatesWithEvents, cancellationToken);

        // Passo 2: traduz domain events para integration events e grava no Outbox.
        // Limpa os eventos dos agregados após enfileirar.
        await _domainEventDispatcher.DispatchToOutboxAsync(aggregatesWithEvents, cancellationToken);

        // Passo 3: persiste tudo (entidades + outbox_messages) em uma única transação EF.
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
