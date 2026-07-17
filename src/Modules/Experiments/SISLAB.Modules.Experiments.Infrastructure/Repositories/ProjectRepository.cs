using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Infrastructure.Persistence;

namespace SISLAB.Modules.Experiments.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProjectRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter; the commit is owned by the unit of work (<c>TransactionBehavior</c> →
/// <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves. The owned batch/group/animal tree is
/// auto-included by its EF navigation configuration, so a single load materializes the whole aggregate.
/// </summary>
internal sealed class ProjectRepository : IProjectRepository
{
    private readonly ExperimentsDbContext _dbContext;

    public ProjectRepository(ExperimentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<Project?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Projects.FirstOrDefaultAsync(project => project.Id == id, ct);

    public async Task AddAsync(Project project, CancellationToken ct = default)
        => await _dbContext.Projects.AddAsync(project, ct);

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        // Tracked aggregates are already observed by the change tracker; Update is an explicit intent guard for
        // detached instances. SaveChanges is owned by the unit of work.
        _dbContext.Projects.Update(project);
        return Task.CompletedTask;
    }
}
