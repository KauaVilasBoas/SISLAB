using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Infrastructure.Persistence;

namespace SISLAB.Modules.Experiments.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISampleRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter; the commit is owned by the unit of work (<c>TransactionBehavior</c> →
/// <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves. The owned analyses collection is
/// auto-included by its EF navigation configuration, so a single load materializes the whole aggregate.
/// </summary>
internal sealed class SampleRepository : ISampleRepository
{
    private readonly ExperimentsDbContext _dbContext;

    public SampleRepository(ExperimentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<Sample?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Samples.FirstOrDefaultAsync(sample => sample.Id == id, ct);

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        string trimmed = code.Trim();
        return await _dbContext.Samples.AnyAsync(sample => sample.Code == trimmed, ct);
    }

    public async Task AddAsync(Sample sample, CancellationToken ct = default)
        => await _dbContext.Samples.AddAsync(sample, ct);

    public Task UpdateAsync(Sample sample, CancellationToken ct = default)
    {
        // Tracked aggregates are already observed by the change tracker; Update is an explicit intent guard for
        // detached instances. SaveChanges is owned by the unit of work.
        _dbContext.Samples.Update(sample);
        return Task.CompletedTask;
    }
}
