namespace SISLAB.Modules.Experiments.Domain.Biobank;

/// <summary>
/// Repository for the <see cref="Sample"/> aggregate (interface in Domain, EF implementation in Infrastructure).
/// Reads are implicitly tenant-scoped by the write-side global query filter; the commit is owned by the unit of
/// work (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves.
/// </summary>
public interface ISampleRepository
{
    /// <summary>Loads a sample (with its analyses) by id, or null when it does not exist for the tenant.</summary>
    Task<Sample?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>True when a sample with the given code already exists for the tenant (code is unique per company).</summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);

    /// <summary>Adds a new sample to the write set.</summary>
    Task AddAsync(Sample sample, CancellationToken ct = default);

    /// <summary>Marks a sample as modified.</summary>
    Task UpdateAsync(Sample sample, CancellationToken ct = default);
}
