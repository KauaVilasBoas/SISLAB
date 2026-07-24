using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Collection;

/// <summary>
/// One assignment on the collection sheet (SISLAB-08): a laboratory member is put in charge of a collection role for the
/// plan — e.g. "Anestesia → Daiane", "Sangue → Sthe". A child of the <see cref="CollectionPlan"/> aggregate, created and
/// removed only through it.
/// </summary>
/// <remarks>
/// <b>Both sides by value.</b> The <see cref="RoleId"/> is a Configuration <c>CollectionRole</c> (the per-tenant role
/// catalogue, SISLAB-08) and the <see cref="UserId"/> is a Lumen user id — both referenced by value, never a
/// cross-module FK or navigation (module isolation, section 2). The role's existence and the user's active membership in
/// the company are validated in the application layer through the Configuration and Identity Contracts ports, mirroring
/// how <c>ExperimentStepResponsible</c> validates a step responsible.
/// </remarks>
public sealed class CollectionRoleAssignment
{
    // Parameterless constructor for EF Core materialization.
    private CollectionRoleAssignment()
    {
    }

    private CollectionRoleAssignment(Guid id, Guid roleId, Guid userId)
    {
        Id = id;
        RoleId = roleId;
        UserId = userId;
    }

    /// <summary>Surrogate key for the assignment row (EF Core tracking); not domain-meaningful.</summary>
    public Guid Id { get; private init; }

    /// <summary>The assigned collection role, referenced by value (Configuration CollectionRole id).</summary>
    public Guid RoleId { get; private init; }

    /// <summary>The member put in charge of the role, referenced by value (Lumen user id).</summary>
    public Guid UserId { get; private set; }

    /// <summary>Creates an assignment linking a role to the member responsible for it.</summary>
    internal static CollectionRoleAssignment Of(Guid roleId, Guid userId)
    {
        Guard.AgainstEmptyGuid(roleId, nameof(roleId));
        Guard.AgainstEmptyGuid(userId, nameof(userId));
        return new CollectionRoleAssignment(Guid.NewGuid(), roleId, userId);
    }

    /// <summary>Whether this assignment is for role <paramref name="roleId"/>.</summary>
    public bool IsForRole(Guid roleId) => RoleId == roleId;

    /// <summary>Reassigns the role to a different member, keeping the role identity.</summary>
    internal void ReassignTo(Guid userId) => UserId = Guard.AgainstEmptyGuid(userId, nameof(userId));
}
