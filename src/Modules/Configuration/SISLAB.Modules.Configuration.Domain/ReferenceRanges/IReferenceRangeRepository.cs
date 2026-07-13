namespace SISLAB.Modules.Configuration.Domain.ReferenceRanges;

/// <summary>
/// Repository for <see cref="ReferenceRange"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface IReferenceRangeRepository
{
    /// <summary>Returns the range with <paramref name="id"/> for the active company, or <see langword="null"/>.</summary>
    Task<ReferenceRange?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a new reference range for the active company.</summary>
    Task AddAsync(ReferenceRange range, CancellationToken ct = default);

    /// <summary>Marks an existing range as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(ReferenceRange range, CancellationToken ct = default);
}
