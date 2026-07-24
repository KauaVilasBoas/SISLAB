namespace SISLAB.Modules.Experiments.Domain.Collection;

/// <summary>
/// Repository for <see cref="CollectionPlan"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface ICollectionPlanRepository
{
    /// <summary>Returns the plan with <paramref name="id"/> for the active company (with its children), or null.</summary>
    Task<CollectionPlan?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the plan for <paramref name="batchId"/> in the active company (with its children), or null.</summary>
    Task<CollectionPlan?> FindByBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Whether a plan already exists for <paramref name="batchId"/> in the active company (one per batch).</summary>
    Task<bool> ExistsForBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Adds a new collection plan for the active company.</summary>
    Task AddAsync(CollectionPlan plan, CancellationToken ct = default);

    /// <summary>Marks an existing plan as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(CollectionPlan plan, CancellationToken ct = default);
}
