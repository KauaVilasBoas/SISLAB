using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Companies;

/// <summary>
/// Aggregate root representing a company (tenant) in SISLAB.
///
/// A Company groups Lumen users (referenced by value via <see cref="CompanyMembership"/>)
/// and serves as the authorization scope for Lumen.Authorization.
///
/// Invariants:
/// - Name cannot be null or empty.
/// - The same user cannot be added to the same company twice.
/// - A user can belong to multiple companies (N:N via CompanyMembership).
/// </summary>
public sealed class Company : AggregateRoot<Guid>
{
    private readonly List<CompanyMembership> _memberships = [];

    // Private constructor for EF Core
    private Company() : base(Guid.Empty) { }

    private Company(Guid id, string name, string? taxId, DateTime createdAt)
        : base(id)
    {
        Name = name;
        TaxId = taxId;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public string Name { get; private set; } = default!;

    public string? TaxId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private init; }

    public IReadOnlyList<CompanyMembership> Memberships => _memberships.AsReadOnly();

    /// <summary>
    /// True when the given Lumen user is a member of this company. Used by profile-assignment use cases to
    /// enforce tenant isolation: a profile may only be assigned to (or removed from) an actual member of the
    /// active company, never a user from another tenant.
    /// </summary>
    public bool IsMember(Guid lumenUserId) => _memberships.Any(m => m.LumenUserId == lumenUserId);

    public static Company Create(string name, string? taxId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name cannot be empty.", nameof(name));

        return new Company(Guid.NewGuid(), name.Trim(), taxId?.Trim(), DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a company with a deterministic identifier.
    /// Reserved for idempotent seed/bootstrap scenarios where the same company must have the
    /// same <see cref="Guid"/> across restarts (existence check by id before re-creating).
    /// </summary>
    public static Company Seed(Guid id, string name, string? taxId = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Seed company Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name cannot be empty.", nameof(name));

        return new Company(id, name.Trim(), taxId?.Trim(), DateTime.UtcNow);
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New name cannot be empty.", nameof(newName));

        Name = newName.Trim();
    }

    public void Activate() => IsActive = true;

    /// <summary>
    /// Deactivates the company. Associated users lose access implicitly via the tenant query filter.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Adds a Lumen user as a member of this company. The userId is the user's identity in Lumen,
    /// referenced by value — no FK to Lumen's schema.
    ///
    /// <para>Membership is a pure link: it grants no permissions by itself. What a member may do in
    /// this company is defined entirely by the Lumen profiles assigned to the user (scoped to this
    /// company) — SISLAB does not model roles.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">User is already a member of this company.</exception>
    public void AddMember(Guid lumenUserId)
    {
        bool alreadyMember = _memberships.Any(m => m.LumenUserId == lumenUserId);
        if (alreadyMember)
            throw new InvalidOperationException(
                $"User '{lumenUserId}' is already a member of company '{Name}'.");

        _memberships.Add(CompanyMembership.Create(Id, lumenUserId));
    }

    /// <summary>
    /// Removes a Lumen user's membership from this company.
    /// </summary>
    /// <exception cref="InvalidOperationException">User is not a member of this company.</exception>
    public void RemoveMember(Guid lumenUserId)
    {
        CompanyMembership? membership = _memberships.FirstOrDefault(m => m.LumenUserId == lumenUserId);
        if (membership is null)
            throw new InvalidOperationException(
                $"User '{lumenUserId}' is not a member of company '{Name}'.");

        _memberships.Remove(membership);
    }

    /// <summary>
    /// Reconstitutes the company from the repository (used by EF Core via navigation loading).
    /// </summary>
    internal void LoadMemberships(IEnumerable<CompanyMembership> memberships)
    {
        _memberships.Clear();
        _memberships.AddRange(memberships);
    }
}
