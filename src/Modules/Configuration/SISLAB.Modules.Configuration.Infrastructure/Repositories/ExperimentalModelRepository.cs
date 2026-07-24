using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExperimentalModelRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The commit is owned by the unit of work.
/// </summary>
internal sealed class ExperimentalModelRepository : IExperimentalModelRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public ExperimentalModelRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ExperimentalModel?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.ExperimentalModels.FirstOrDefaultAsync(model => model.Id == id, ct);

    public async Task AddAsync(ExperimentalModel model, CancellationToken ct = default)
        => await _dbContext.ExperimentalModels.AddAsync(model, ct);

    public Task UpdateAsync(ExperimentalModel model, CancellationToken ct = default)
    {
        _dbContext.ExperimentalModels.Update(model);
        return Task.CompletedTask;
    }
}
