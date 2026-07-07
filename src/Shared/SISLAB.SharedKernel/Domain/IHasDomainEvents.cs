namespace SISLAB.SharedKernel.Domain;

/// <summary>
/// Interface não-genérica que expõe os domain events de um agregado.
/// Permite que a infraestrutura (ChangeTracker, UnitOfWork) colete eventos de qualquer
/// <see cref="AggregateRoot{TId}"/> sem precisar conhecer o tipo do identificador.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Domain events pendentes de despacho.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Remove todos os eventos da fila interna após o despacho.</summary>
    void ClearDomainEvents();
}
