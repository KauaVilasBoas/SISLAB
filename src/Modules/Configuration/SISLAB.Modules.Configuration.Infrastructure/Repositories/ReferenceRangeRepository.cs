using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.ReferenceRanges;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IReferenceRangeRepository"/>. Reads are implicitly tenant-scoped by
/// the write-side global query filter. The commit is owned by the unit of work.
/// </summary>
internal sealed class ReferenceRangeRepository : IReferenceRangeRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public ReferenceRangeRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ReferenceRange?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.ReferenceRanges.FirstOrDefaultAsync(range => range.Id == id, ct);

    public async Task AddAsync(ReferenceRange range, CancellationToken ct = default)
        => await _dbContext.ReferenceRanges.AddAsync(range, ct);

    public Task UpdateAsync(ReferenceRange range, CancellationToken ct = default)
    {
        _dbContext.ReferenceRanges.Update(range);
        return Task.CompletedTask;
    }
}
