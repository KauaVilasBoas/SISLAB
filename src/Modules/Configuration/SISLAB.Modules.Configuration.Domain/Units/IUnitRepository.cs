namespace SISLAB.Modules.Configuration.Domain.Units;

/// <summary>
/// Repository for <see cref="Unit"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface IUnitRepository
{
    /// <summary>Returns the unit with <paramref name="id"/> for the active company, or <see langword="null"/>.</summary>
    Task<Unit?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the active company's unit whose symbol matches (case-insensitively), or null.</summary>
    Task<Unit?> FindBySymbolAsync(string symbol, CancellationToken ct = default);

    /// <summary>Adds a new unit for the active company.</summary>
    Task AddAsync(Unit unit, CancellationToken ct = default);

    /// <summary>Marks an existing unit as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(Unit unit, CancellationToken ct = default);
}
