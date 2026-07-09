using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICompanyRepository"/>.
/// Loads memberships via Include so the aggregate is reconstituted fully.
/// </summary>
internal sealed class CompanyRepository : ICompanyRepository
{
    private readonly IdentityDbContext _dbContext;

    public CompanyRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Companies
            .Include(c => c.Memberships)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default)
        => await _dbContext.Companies
            .Include(c => c.Memberships)
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default)
        => await _dbContext.Companies
            .Include(c => c.Memberships)
            .Where(c => c.IsActive && c.Memberships.Any(m => m.LumenUserId == lumenUserId))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default)
        => _dbContext.Companies
            .Where(c => c.Id == companyId && c.IsActive)
            .SelectMany(c => c.Memberships)
            .AnyAsync(m => m.LumenUserId == lumenUserId, ct);

    public async Task AddAsync(Company company, CancellationToken ct = default)
        => await _dbContext.Companies.AddAsync(company, ct);

    public Task UpdateAsync(Company company, CancellationToken ct = default)
    {
        _dbContext.Companies.Update(company);
        return Task.CompletedTask;
    }
}
