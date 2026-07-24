using SISLAB.Modules.Experiments.Domain.Collection;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ICollectionPlanRepository"/> test double (SISLAB-08). Mirrors the module's other repository
/// fakes: it stores plans by id, tracks the last added/updated instance and answers the one-per-batch existence guard.
/// </summary>
internal sealed class FakeCollectionPlanRepository : ICollectionPlanRepository
{
    private readonly Dictionary<Guid, CollectionPlan> _store = new();

    public CollectionPlan? LastAdded { get; private set; }

    public CollectionPlan? LastUpdated { get; private set; }

    public FakeCollectionPlanRepository Seed(CollectionPlan plan)
    {
        _store[plan.Id] = plan;
        return this;
    }

    public Task<CollectionPlan?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<CollectionPlan?> FindByBatchAsync(Guid batchId, CancellationToken ct = default)
        => Task.FromResult(_store.Values.FirstOrDefault(plan => plan.BatchId == batchId));

    public Task<bool> ExistsForBatchAsync(Guid batchId, CancellationToken ct = default)
        => Task.FromResult(_store.Values.Any(plan => plan.BatchId == batchId));

    public Task AddAsync(CollectionPlan plan, CancellationToken ct = default)
    {
        _store[plan.Id] = plan;
        LastAdded = plan;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CollectionPlan plan, CancellationToken ct = default)
    {
        _store[plan.Id] = plan;
        LastUpdated = plan;
        return Task.CompletedTask;
    }
}
