using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.Modules.Inventory.Infrastructure.Persistence;

namespace SISLAB.Modules.Inventory.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPartnerRepository"/>. Reads are implicitly tenant-scoped by the
/// write-side global query filter; owned samples load with the aggregate. The commit is owned by the unit
/// of work (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never
/// saves.
/// </summary>
internal sealed class PartnerRepository : IPartnerRepository
{
    private readonly InventoryDbContext _dbContext;

    public PartnerRepository(InventoryDbContext dbContext) => _dbContext = dbContext;

    public async Task<Partner?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Partners.FirstOrDefaultAsync(partner => partner.Id == id, ct);

    public async Task AddAsync(Partner partner, CancellationToken ct = default)
        => await _dbContext.Partners.AddAsync(partner, ct);

    public Task UpdateAsync(Partner partner, CancellationToken ct = default)
    {
        _dbContext.Partners.Update(partner);
        return Task.CompletedTask;
    }
}
