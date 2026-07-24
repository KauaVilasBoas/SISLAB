using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.InclusionCriteria;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IInclusionCriterionRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The commit is owned by the unit of work.
/// </summary>
internal sealed class InclusionCriterionRepository : IInclusionCriterionRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public InclusionCriterionRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<InclusionCriterion?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.InclusionCriteria.FirstOrDefaultAsync(criterion => criterion.Id == id, ct);

    public async Task<bool> ParameterExistsAsync(string parameterCode, CancellationToken ct = default)
    {
        string normalized = parameterCode.Trim();

        return await _dbContext.InclusionCriteria
            .AnyAsync(criterion => criterion.ParameterCode.ToLower() == normalized.ToLower(), ct);
    }

    public async Task AddAsync(InclusionCriterion criterion, CancellationToken ct = default)
        => await _dbContext.InclusionCriteria.AddAsync(criterion, ct);

    public Task UpdateAsync(InclusionCriterion criterion, CancellationToken ct = default)
    {
        _dbContext.InclusionCriteria.Update(criterion);
        return Task.CompletedTask;
    }
}
