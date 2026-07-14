using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IItemCategoryRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The commit is owned by the unit of work.
/// </summary>
internal sealed class ItemCategoryRepository : IItemCategoryRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public ItemCategoryRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ItemCategory?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.ItemCategories.FirstOrDefaultAsync(category => category.Id == id, ct);

    public async Task<ItemCategory?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        string normalized = name.Trim();
        return await _dbContext.ItemCategories
            .FirstOrDefaultAsync(category => category.Name.ToLower() == normalized.ToLower(), ct);
    }

    public async Task AddAsync(ItemCategory category, CancellationToken ct = default)
        => await _dbContext.ItemCategories.AddAsync(category, ct);

    public Task UpdateAsync(ItemCategory category, CancellationToken ct = default)
    {
        _dbContext.ItemCategories.Update(category);
        return Task.CompletedTask;
    }
}
