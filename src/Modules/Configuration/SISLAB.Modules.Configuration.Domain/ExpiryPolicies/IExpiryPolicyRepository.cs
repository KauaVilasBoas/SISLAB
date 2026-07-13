namespace SISLAB.Modules.Configuration.Domain.ExpiryPolicies;

/// <summary>
/// Repository for the tenant's <see cref="ExpiryPolicy"/> (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter, so there is a
/// single policy to find for the active company.
/// </summary>
public interface IExpiryPolicyRepository
{
    /// <summary>Returns the active company's expiry policy, or <see langword="null"/> when none is configured yet.</summary>
    Task<ExpiryPolicy?> GetAsync(CancellationToken ct = default);

    /// <summary>Adds a new expiry policy for the active company.</summary>
    Task AddAsync(ExpiryPolicy policy, CancellationToken ct = default);

    /// <summary>Marks an existing policy as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(ExpiryPolicy policy, CancellationToken ct = default);
}
