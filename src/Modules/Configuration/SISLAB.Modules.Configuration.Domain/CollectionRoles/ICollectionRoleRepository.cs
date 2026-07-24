namespace SISLAB.Modules.Configuration.Domain.CollectionRoles;

/// <summary>
/// Repository for <see cref="CollectionRole"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface ICollectionRoleRepository
{
    /// <summary>Returns the role with <paramref name="id"/> for the active company, or <see langword="null"/>.</summary>
    Task<CollectionRole?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Whether a role already exists with <paramref name="name"/> in the active company (the one-per-name
    /// uniqueness pre-check; the DB unique index is the final guard).
    /// </summary>
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);

    /// <summary>Adds a new collection role for the active company.</summary>
    Task AddAsync(CollectionRole role, CancellationToken ct = default);

    /// <summary>Marks an existing role as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(CollectionRole role, CancellationToken ct = default);
}
