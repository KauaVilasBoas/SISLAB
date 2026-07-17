using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Infrastructure.Persistence;

namespace SISLAB.Modules.Experiments.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExperimentRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter; the commit is owned by the unit of work (<c>TransactionBehavior</c> →
/// <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves. The owned steps/plate/wells are
/// auto-included by their EF navigation configuration, so a single load materializes the whole aggregate.
/// </summary>
internal sealed class ExperimentRepository : IExperimentRepository
{
    private readonly ExperimentsDbContext _dbContext;

    public ExperimentRepository(ExperimentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<Experiment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Experiments.FirstOrDefaultAsync(experiment => experiment.Id == id, ct);

    public async Task<PlateExperiment?> FindPlateExperimentWithPlateAsync(
        Guid id,
        CancellationToken ct = default)
        => await _dbContext.Experiments
            .OfType<PlateExperiment>()
            .FirstOrDefaultAsync(experiment => experiment.Id == id, ct);

    public async Task AddAsync(Experiment experiment, CancellationToken ct = default)
        => await _dbContext.Experiments.AddAsync(experiment, ct);

    public Task UpdateAsync(Experiment experiment, CancellationToken ct = default)
    {
        // Tracked aggregates are already observed by the change tracker; Update is an explicit intent guard for
        // detached instances. SaveChanges is owned by the unit of work.
        _dbContext.Experiments.Update(experiment);
        return Task.CompletedTask;
    }
}
