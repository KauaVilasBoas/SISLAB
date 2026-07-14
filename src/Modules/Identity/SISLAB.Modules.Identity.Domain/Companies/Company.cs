using SISLAB.Modules.Identity.Domain.Companies.Events;
using SISLAB.SharedKernel.Authorization;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

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
    /// Adds a Lumen user as a member of this company with the given <paramref name="role"/>.
    /// The userId is the user's identity in Lumen, referenced by value — no FK to Lumen's schema.
    /// New members default to <see cref="Role.ReadOnly"/> (least privilege); the company owner set
    /// at provisioning is added explicitly as <see cref="Role.Coordinator"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">User is already a member of this company.</exception>
    public void AddMember(Guid lumenUserId, Role role = Role.ReadOnly)
    {
        bool alreadyMember = _memberships.Any(m => m.LumenUserId == lumenUserId);
        if (alreadyMember)
            throw new InvalidOperationException(
                $"User '{lumenUserId}' is already a member of company '{Name}'.");

        _memberships.Add(CompanyMembership.Create(Id, lumenUserId, role));
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
    /// Reassigns the business <see cref="Role"/> of an existing member.
    ///
    /// <para>Enforces the aggregate invariant <b>"a company must always retain at least one active
    /// Coordinator"</b>: demoting the last remaining Coordinator to any other role is rejected with a
    /// <see cref="BusinessException"/>. Assigning the same role a member already holds is a no-op that
    /// raises no event (idempotency, avoiding spurious Lumen Profile churn downstream).</para>
    ///
    /// <para>On a real change, raises <see cref="MemberRoleChangedEvent"/> so the Role→Lumen-Profile
    /// translation (#77d) can reconcile the member's scoped Profile assignment.</para>
    /// </summary>
    /// <exception cref="BusinessException">
    /// The user is not a member of this company, or the change would leave the company with no
    /// active Coordinator.
    /// </exception>
    public void AssignMemberRole(Guid lumenUserId, Role newRole)
    {
        CompanyMembership? membership = _memberships.FirstOrDefault(m => m.LumenUserId == lumenUserId);
        if (membership is null)
            throw new BusinessException(
                $"User '{lumenUserId}' is not a member of company '{Name}'.");

        Role previousRole = membership.Role;
        if (previousRole == newRole)
            return;

        bool isDemotingACoordinator = previousRole == Role.Coordinator && newRole != Role.Coordinator;
        if (isDemotingACoordinator && CountCoordinators() == 1)
            throw new BusinessException(
                $"Company '{Name}' must retain at least one Coordinator. " +
                "Promote another member to Coordinator before changing this one's role.");

        membership.ChangeRole(newRole);

        RaiseDomainEvent(new MemberRoleChangedEvent(Id, lumenUserId, previousRole, newRole));
    }

    private int CountCoordinators() => _memberships.Count(m => m.Role == Role.Coordinator);

    /// <summary>
    /// Reconstitutes the company from the repository (used by EF Core via navigation loading).
    /// </summary>
    internal void LoadMemberships(IEnumerable<CompanyMembership> memberships)
    {
        _memberships.Clear();
        _memberships.AddRange(memberships);
    }
}
