namespace SISLAB.Infrastructure.Persistence;

/// <summary>
/// Unidade de trabalho: encapsula a transação EF Core de um módulo.
/// A implementação concreta (<see cref="EfUnitOfWork{TContext}"/>) é responsável por:
/// 1. Despachar domain events dos agregados modificados (E2 — a ser implementado).
/// 2. Gravar mensagens no Outbox antes de confirmar (E2 — a ser implementado).
/// 3. Chamar SaveChangesAsync no DbContext do módulo.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persiste todas as mudanças da unidade de trabalho corrente.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
