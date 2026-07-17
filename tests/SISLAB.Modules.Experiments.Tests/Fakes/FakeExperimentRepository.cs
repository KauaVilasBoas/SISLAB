using SISLAB.Modules.Experiments.Domain.Experiments;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>
/// In-memory fake of <see cref="IExperimentRepository"/> for handler unit tests — no EF, no database. Records
/// the last added/updated aggregate so tests can assert on the persisted instance.
/// </summary>
internal sealed class FakeExperimentRepository : IExperimentRepository
{
    private readonly Dictionary<Guid, Experiment> _store = new();

    public Experiment? LastAdded { get; private set; }

    public Experiment? LastUpdated { get; private set; }

    public FakeExperimentRepository Seed(Experiment experiment)
    {
        _store[experiment.Id] = experiment;
        return this;
    }

    public Task<Experiment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<PlateExperiment?> FindPlateExperimentWithPlateAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.GetValueOrDefault(id) as PlateExperiment);

    public Task AddAsync(Experiment experiment, CancellationToken ct = default)
    {
        _store[experiment.Id] = experiment;
        LastAdded = experiment;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Experiment experiment, CancellationToken ct = default)
    {
        _store[experiment.Id] = experiment;
        LastUpdated = experiment;
        return Task.CompletedTask;
    }
}
