namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// Repository for <see cref="ExperimentalModel"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface IExperimentalModelRepository
{
    /// <summary>Returns the model with <paramref name="id"/> for the active company, or <see langword="null"/>.</summary>
    Task<ExperimentalModel?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a new experimental model for the active company.</summary>
    Task AddAsync(ExperimentalModel model, CancellationToken ct = default);

    /// <summary>Marks an existing model as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(ExperimentalModel model, CancellationToken ct = default);
}
