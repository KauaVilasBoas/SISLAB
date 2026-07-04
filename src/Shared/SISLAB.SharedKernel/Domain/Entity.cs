namespace SISLAB.SharedKernel.Domain;

/// <summary>
/// Entidade de domínio com igualdade baseada em identidade.
/// Duas entidades são iguais se e somente se possuem o mesmo tipo e o mesmo ID.
/// </summary>
/// <typeparam name="TId">Tipo do identificador da entidade.</typeparam>
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>Identificador único da entidade.</summary>
    public TId Id { get; private init; }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        => !(left == right);
}
