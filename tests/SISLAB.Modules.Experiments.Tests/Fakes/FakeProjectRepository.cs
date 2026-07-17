using SISLAB.Modules.Experiments.Domain.Projects;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>
/// In-memory fake of <see cref="IProjectRepository"/> for handler unit tests — no EF, no database. Records the
/// last added/updated aggregate so tests can assert on the persisted instance.
/// </summary>
internal sealed class FakeProjectRepository : IProjectRepository
{
    private readonly Dictionary<Guid, Project> _store = new();

    public Project? LastAdded { get; private set; }

    public Project? LastUpdated { get; private set; }

    public FakeProjectRepository Seed(Project project)
    {
        _store[project.Id] = project;
        return this;
    }

    public Task<Project?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.GetValueOrDefault(id));

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _store[project.Id] = project;
        LastAdded = project;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        _store[project.Id] = project;
        LastUpdated = project;
        return Task.CompletedTask;
    }
}
