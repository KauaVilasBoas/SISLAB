namespace SISLAB.Modules.Configuration.Domain.InclusionCriteria;

/// <summary>
/// Repository for <see cref="InclusionCriterion"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface IInclusionCriterionRepository
{
    /// <summary>Returns the criterion with <paramref name="id"/> for the active company, or <see langword="null"/>.</summary>
    Task<InclusionCriterion?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Whether a criterion already exists for <paramref name="parameterCode"/> in the active company (the
    /// one-per-parameter uniqueness pre-check; the DB unique index is the final guard).
    /// </summary>
    Task<bool> ParameterExistsAsync(string parameterCode, CancellationToken ct = default);

    /// <summary>Adds a new inclusion criterion for the active company.</summary>
    Task AddAsync(InclusionCriterion criterion, CancellationToken ct = default);

    /// <summary>Marks an existing criterion as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(InclusionCriterion criterion, CancellationToken ct = default);
}
