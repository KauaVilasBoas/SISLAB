namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// Repository for the <see cref="Company"/> aggregate.
/// Concrete implementation lives in the module's Infrastructure project.
/// </summary>
public interface ICompanyRepository
{
    Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists active companies the Lumen user belongs to (via <c>company_memberships</c>),
    /// ordered by name. Used post-login to resolve the user's available companies.
    /// </summary>
    Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether the Lumen user is an active member of the given company.
    /// Used by the tenant middleware and by company activation to validate membership.
    /// </summary>
    Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default);

    Task AddAsync(Company company, CancellationToken ct = default);

    Task UpdateAsync(Company company, CancellationToken ct = default);

    /// <summary>
    /// Persists pending changes to tracked aggregates in a single transaction. Exposed on the repository
    /// (rather than a module-wide <c>IUnitOfWork</c>) because the Identity write surface is small — member
    /// administration — and does not use the Outbox/domain-event pipeline the Inventory module wires up.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
