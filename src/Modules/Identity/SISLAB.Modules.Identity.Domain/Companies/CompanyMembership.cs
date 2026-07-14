using SISLAB.SharedKernel.Authorization;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// N:N association entity between a <see cref="Company"/> and a Lumen user.
///
/// The Lumen userId is stored by value (<see cref="LumenUserId"/>) — no FK,
/// no navigation to Lumen's tables. This enforces bounded-context isolation:
/// SISLAB has no knowledge of the identity system's internal schema.
///
/// A <see cref="CompanyMembership"/> belongs to the <see cref="Company"/> aggregate
/// and must not be mutated outside it — hence the <c>internal</c> mutators.
/// </summary>
public sealed class CompanyMembership : Entity<Guid>
{
    // Private constructor for EF Core
    private CompanyMembership() : base(Guid.Empty) { }

    private CompanyMembership(Guid id, Guid companyId, Guid lumenUserId, Role role, DateTime joinedAt)
        : base(id)
    {
        CompanyId = companyId;
        LumenUserId = lumenUserId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid CompanyId { get; private init; }

    /// <summary>
    /// Lumen user identifier, referenced by value — no FK to avoid cross-boundary schema coupling.
    /// </summary>
    public Guid LumenUserId { get; private init; }

    /// <summary>
    /// Business role the member holds within the company. Drives the Lumen Profile assignment,
    /// scoped to <see cref="CompanyId"/>. Mutated only by the owning <see cref="Company"/> aggregate.
    /// </summary>
    public Role Role { get; private set; }

    public DateTime JoinedAt { get; private init; }

    internal static CompanyMembership Create(Guid companyId, Guid lumenUserId, Role role)
        => new(Guid.NewGuid(), companyId, lumenUserId, role, DateTime.UtcNow);

    /// <summary>
    /// Reassigns the member's role. Called only by the <see cref="Company"/> aggregate, which owns
    /// the ≥1-active-Coordinator invariant; this method performs no invariant checking on its own.
    /// </summary>
    internal void ChangeRole(Role role) => Role = role;
}
