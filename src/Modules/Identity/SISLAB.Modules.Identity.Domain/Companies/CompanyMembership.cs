using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// N:N association entity between a <see cref="Company"/> and a Lumen user.
///
/// The Lumen userId is stored by value (<see cref="LumenUserId"/>) — no FK,
/// no navigation to Lumen's tables. This enforces bounded-context isolation:
/// SISLAB has no knowledge of the identity system's internal schema.
///
/// <para>This is a <b>pure membership link</b> (user ↔ company). It carries no authorization
/// concept of its own: which permissions a member holds in a company is owned entirely by Lumen
/// (Lumen profiles assigned to the user, scoped to the company). SISLAB does not model roles.</para>
///
/// A <see cref="CompanyMembership"/> belongs to the <see cref="Company"/> aggregate
/// and must not be mutated outside it — hence the <c>internal</c> factory.
/// </summary>
public sealed class CompanyMembership : Entity<Guid>
{
    // Private constructor for EF Core
    private CompanyMembership() : base(Guid.Empty) { }

    private CompanyMembership(Guid id, Guid companyId, Guid lumenUserId, DateTime joinedAt)
        : base(id)
    {
        CompanyId = companyId;
        LumenUserId = lumenUserId;
        JoinedAt = joinedAt;
    }

    public Guid CompanyId { get; private init; }

    /// <summary>
    /// Lumen user identifier, referenced by value — no FK to avoid cross-boundary schema coupling.
    /// </summary>
    public Guid LumenUserId { get; private init; }

    public DateTime JoinedAt { get; private init; }

    internal static CompanyMembership Create(Guid companyId, Guid lumenUserId)
        => new(Guid.NewGuid(), companyId, lumenUserId, DateTime.UtcNow);
}
