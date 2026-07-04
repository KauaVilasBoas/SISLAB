using SISLAB.SharedKernel.Domain;

namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Despacha domain events coletados nos agregados após a persistência.
/// Chamado pela infraestrutura (UnitOfWork) imediatamente antes ou após SaveChanges,
/// de acordo com a estratégia de consistência escolhida para cada evento.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Despacha todos os eventos pendentes de uma coleção de agregados.
    /// </summary>
    Task DispatchAndClearAsync(IEnumerable<AggregateRoot<Guid>> aggregates, CancellationToken cancellationToken = default);
}
