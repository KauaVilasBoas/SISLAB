using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.Units;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUnitRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter. The commit is owned by the unit of work.
/// </summary>
internal sealed class UnitRepository : IUnitRepository
{
    private readonly ConfigurationDbContext _dbContext;

    public UnitRepository(ConfigurationDbContext dbContext) => _dbContext = dbContext;

    public async Task<Unit?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Units.FirstOrDefaultAsync(unit => unit.Id == id, ct);

    public async Task<Unit?> FindBySymbolAsync(string symbol, CancellationToken ct = default)
    {
        string normalized = symbol.Trim();
        return await _dbContext.Units
            .FirstOrDefaultAsync(unit => unit.Symbol.ToLower() == normalized.ToLower(), ct);
    }

    public async Task AddAsync(Unit unit, CancellationToken ct = default)
        => await _dbContext.Units.AddAsync(unit, ct);

    public Task UpdateAsync(Unit unit, CancellationToken ct = default)
    {
        _dbContext.Units.Update(unit);
        return Task.CompletedTask;
    }
}
