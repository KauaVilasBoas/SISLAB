namespace SISLAB.Modules.Inventory.Domain.Partners;

/// <summary>
/// Repository for the <see cref="Partner"/> aggregate. The concrete implementation lives in the module's
/// Infrastructure project (EF Core). All lookups are implicitly tenant-scoped by the write-side global
/// query filter.
/// </summary>
public interface IPartnerRepository
{
    Task<Partner?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Partner partner, CancellationToken ct = default);

    Task UpdateAsync(Partner partner, CancellationToken ct = default);
}
