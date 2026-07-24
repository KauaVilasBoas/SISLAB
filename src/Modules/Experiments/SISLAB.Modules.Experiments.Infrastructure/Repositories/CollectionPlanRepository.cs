using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.Modules.Experiments.Infrastructure.Persistence;

namespace SISLAB.Modules.Experiments.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICollectionPlanRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter; the commit is owned by the unit of work. The owned routings/planned-analyses and
/// role assignments are auto-included by their EF navigation configuration, so a single load materializes the whole
/// aggregate.
/// </summary>
internal sealed class CollectionPlanRepository : ICollectionPlanRepository
{
    private readonly ExperimentsDbContext _dbContext;

    public CollectionPlanRepository(ExperimentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<CollectionPlan?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.CollectionPlans.FirstOrDefaultAsync(plan => plan.Id == id, ct);

    public async Task<CollectionPlan?> FindByBatchAsync(Guid batchId, CancellationToken ct = default)
        => await _dbContext.CollectionPlans.FirstOrDefaultAsync(plan => plan.BatchId == batchId, ct);

    public async Task<bool> ExistsForBatchAsync(Guid batchId, CancellationToken ct = default)
        => await _dbContext.CollectionPlans.AnyAsync(plan => plan.BatchId == batchId, ct);

    public async Task AddAsync(CollectionPlan plan, CancellationToken ct = default)
        => await _dbContext.CollectionPlans.AddAsync(plan, ct);

    public Task UpdateAsync(CollectionPlan plan, CancellationToken ct = default)
    {
        _dbContext.CollectionPlans.Update(plan);
        return Task.CompletedTask;
    }
}
