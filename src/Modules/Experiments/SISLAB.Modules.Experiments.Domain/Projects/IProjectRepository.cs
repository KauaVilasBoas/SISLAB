namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// Repository for the <see cref="Project"/> aggregate (interface in Domain, EF implementation in Infrastructure).
/// Reads are implicitly tenant-scoped by the write-side global query filter; the commit is owned by the unit of
/// work (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves. The
/// whole batch/group/animal tree loads with the root (owned navigations are auto-included).
/// </summary>
public interface IProjectRepository
{
    /// <summary>Loads a project (with its batches, groups and animals) by id, or null when absent for the tenant.</summary>
    Task<Project?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a new project to the write set.</summary>
    Task AddAsync(Project project, CancellationToken ct = default);

    /// <summary>Marks a project as modified.</summary>
    Task UpdateAsync(Project project, CancellationToken ct = default);
}
