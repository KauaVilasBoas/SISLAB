namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// Repository for the <see cref="Experiment"/> aggregate (interface in Domain, EF implementation in
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter; the commit is
/// owned by the unit of work (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the
/// repository never saves.
/// </summary>
public interface IExperimentRepository
{
    /// <summary>Loads an experiment (with its steps) by id, or null when it does not exist for the tenant.</summary>
    Task<Experiment?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads a plate experiment (viability or nitric oxide) with its full plate (wells) eagerly, or null when it
    /// does not exist for the tenant or is not a plate experiment. Used by the plate-design / reading / calculation
    /// commands, which operate on the shared <see cref="PlateExperiment"/> lifecycle regardless of the assay type.
    /// </summary>
    Task<PlateExperiment?> FindPlateExperimentWithPlateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads a behavioural experiment (von Frey / tail-flick / rota-rod / hemogram) with its measurements eagerly,
    /// or null when it does not exist for the tenant or is not a behavioural experiment. Used by the timepoint
    /// launch and calculation commands, which operate on the shared <see cref="BehavioralExperiment"/> lifecycle
    /// regardless of the assay type.
    /// </summary>
    Task<BehavioralExperiment?> FindBehavioralExperimentAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a new experiment to the write set.</summary>
    Task AddAsync(Experiment experiment, CancellationToken ct = default);

    /// <summary>Marks an experiment as modified.</summary>
    Task UpdateAsync(Experiment experiment, CancellationToken ct = default);
}
