using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação concreta do <see cref="ICompanyRepository"/> usando EF Core.
/// Carrega memberships via Include para que o agregado seja reconstituído completo.
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

    public async Task AddAsync(Company company, CancellationToken ct = default)
        => await _dbContext.Companies.AddAsync(company, ct);

    public Task UpdateAsync(Company company, CancellationToken ct = default)
    {
        _dbContext.Companies.Update(company);
        return Task.CompletedTask;
    }
}
