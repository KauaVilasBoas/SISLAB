using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExpiryPolicyRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter, so there is a single policy to find for the active company. The commit is
/// owned by the unit of work (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>).
/// </summary>
internal sealed class ExpiryPolicyRepository : IExpiryPolicyRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public ExpiryPolicyRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ExpiryPolicy?> GetAsync(CancellationToken ct = default)
        => await _dbContext.ExpiryPolicies.FirstOrDefaultAsync(ct);

    public async Task AddAsync(ExpiryPolicy policy, CancellationToken ct = default)
        => await _dbContext.ExpiryPolicies.AddAsync(policy, ct);

    public Task UpdateAsync(ExpiryPolicy policy, CancellationToken ct = default)
    {
        _dbContext.ExpiryPolicies.Update(policy);
        return Task.CompletedTask;
    }
}
