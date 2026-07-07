namespace SISLAB.SharedKernel.Domain;

/// <summary>
/// Raiz de agregado. Além da igualdade por identidade de <see cref="Entity{TId}"/>,
/// mantém uma coleção interna de <see cref="IDomainEvent"/> ocorridos durante a operação.
/// Os eventos são coletados por infraestrutura (UnitOfWork) após cada SaveChanges,
/// despachados e então limpos com <see cref="ClearDomainEvents"/>.
/// </summary>
/// <typeparam name="TId">Tipo do identificador do agregado.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Eventos de domínio ocorridos neste agregado, expostos como somente-leitura.
    /// Infraestrutura acessa esta lista para despachar; nunca muta externamente.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Registra um novo evento de domínio a ser despachado após a persistência.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Remove todos os eventos da fila interna. Chamado pela infraestrutura
    /// após o despacho bem-sucedido dos eventos.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
