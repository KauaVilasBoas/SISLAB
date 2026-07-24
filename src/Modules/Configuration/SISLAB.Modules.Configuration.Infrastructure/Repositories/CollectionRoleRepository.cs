using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.CollectionRoles;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICollectionRoleRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The commit is owned by the unit of work.
/// </summary>
internal sealed class CollectionRoleRepository : ICollectionRoleRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public CollectionRoleRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<CollectionRole?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.CollectionRoles.FirstOrDefaultAsync(role => role.Id == id, ct);

    public async Task<bool> NameExistsAsync(string name, CancellationToken ct = default)
    {
        string normalized = name.Trim();

        return await _dbContext.CollectionRoles
            .AnyAsync(role => role.Name.ToLower() == normalized.ToLower(), ct);
    }

    public async Task AddAsync(CollectionRole role, CancellationToken ct = default)
        => await _dbContext.CollectionRoles.AddAsync(role, ct);

    public Task UpdateAsync(CollectionRole role, CancellationToken ct = default)
    {
        _dbContext.CollectionRoles.Update(role);
        return Task.CompletedTask;
    }
}
