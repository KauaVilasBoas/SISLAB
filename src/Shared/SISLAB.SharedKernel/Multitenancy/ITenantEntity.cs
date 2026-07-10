namespace SISLAB.SharedKernel.Multitenancy;

/// <summary>
/// Marker interface for tenant-scoped entities in SISLAB.
///
/// Every entity that stores tenant-owned data exposes its owning company through
/// <see cref="CompanyId"/>. The write-side multi-tenancy machinery relies on this marker:
/// <list type="bullet">
///   <item>the EF Core global query filter narrows all reads to the active company;</item>
///   <item>a save interceptor stamps <see cref="CompanyId"/> on new rows and guards against
///   writing data belonging to a different company (cross-tenant leak).</item>
/// </list>
///
/// This is the write-side half of the defense-in-depth strategy; the read-side (Dapper)
/// enforces the same rule with an explicit <c>WHERE company_id = @CompanyId</c>.
///
/// The setter is deliberately kept out of this contract: <see cref="CompanyId"/> is assigned
/// by the persistence interceptor, not by application code, so that developers cannot forget
/// (or tamper with) the tenant stamp. Concrete entities expose a private/init setter mapped
/// by EF Core configuration.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Company (tenant) that owns this entity. Never <see cref="System.Guid.Empty"/> once persisted.
    /// </summary>
    Guid CompanyId { get; }
}
