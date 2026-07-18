using SISLAB.Modules.Experiments.Domain.Biobank;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>
/// In-memory fake of <see cref="ISampleRepository"/> for handler unit tests — no EF, no database. Records the
/// last added/updated aggregate so tests can assert on the persisted instance.
/// </summary>
internal sealed class FakeSampleRepository : ISampleRepository
{
    private readonly Dictionary<Guid, Sample> _store = new();

    public Sample? LastAdded { get; private set; }

    public Sample? LastUpdated { get; private set; }

    public FakeSampleRepository Seed(Sample sample)
    {
        _store[sample.Id] = sample;
        return this;
    }

    public Task<Sample?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_store.Values.Any(s =>
            string.Equals(s.Code, code.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Sample sample, CancellationToken ct = default)
    {
        _store[sample.Id] = sample;
        LastAdded = sample;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Sample sample, CancellationToken ct = default)
    {
        _store[sample.Id] = sample;
        LastUpdated = sample;
        return Task.CompletedTask;
    }
}
