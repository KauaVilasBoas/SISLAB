using Microsoft.EntityFrameworkCore;

namespace SISLAB.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> baseada em EF Core.
/// Genérica para que cada módulo instancie com seu próprio DbContext derivado.
///
/// PONTO DE EXTENSÃO (E2):
/// Antes do SaveChangesAsync, o E2 irá:
/// 1. Coletar agregados modificados com domain events pendentes via ChangeTracker.
/// 2. Chamar IDomainEventDispatcher para despachar eventos síncronos (invariantes).
/// 3. Gravar integration events no Outbox (na mesma transação).
/// Apenas após essas etapas SaveChangesAsync será invocado.
/// </summary>
/// <typeparam name="TContext">DbContext derivado do módulo.</typeparam>
public sealed class EfUnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    private readonly TContext _dbContext;

    public EfUnitOfWork(TContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // TODO (E2): Inserir aqui o despacho de domain events e gravação de Outbox
        // antes de delegar ao DbContext.
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
