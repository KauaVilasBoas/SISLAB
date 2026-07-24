using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Collection.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Domain.Collection;

/// <summary>
/// The collection plan for a batch (leva) — SISLAB-08, the digital collection sheet of the in vivo spreadsheet. It
/// captures, per batch, two things the operators keep by hand today: the <b>matrix</b> that routes each sample type to
/// its planned analyses and storage (<see cref="Routings"/>), and the <b>roster</b> that puts a member in charge of each
/// collection role (<see cref="Assignments"/> — Volante, Anestesia, Decapitação, Sangue, …).
/// </summary>
/// <remarks>
/// <para>
/// <b>Own aggregate, ids by value.</b> The plan is its own aggregate root, keyed within a tenant by the batch it plans
/// for (<see cref="ProjectId"/> + <see cref="BatchId"/>, one plan per batch). The project/batch it belongs to, the
/// Configuration rooms it stores into and the Configuration roles it assigns are all held only by their id — no
/// cross-aggregate/cross-module FK or navigation (module isolation, section 2), exactly like the rest of the module.
/// </para>
/// <para>
/// <b>Why here and not in Configuration.</b> A routing is expressed over the biobank's <see cref="SampleType"/> and is
/// bound to a concrete batch/experiment; it is the plan that drives the real <c>Sample.Analyse</c>. That is operational
/// planning of a study, not a per-tenant setting, so it lives in the Experiments biobank area next to the samples it
/// governs. The <i>role catalogue</i> it assigns, by contrast, IS a per-tenant setting and stays in Configuration; the
/// plan references a role only by value.
/// </para>
/// <para>
/// <b>No parallel status.</b> The plan deliberately holds no "done/pending" flag on a routing or analysis. The status
/// board is a read-side derivation that matches each planned analysis, by name, to the sample's real biobank analyses,
/// so the board always reflects the actual state and can never drift out of sync with the biobank.
/// </para>
/// </remarks>
public sealed class CollectionPlan : AggregateRoot<Guid>, ITenantEntity
{
    private readonly List<SampleRouting> _routings = [];
    private readonly List<CollectionRoleAssignment> _assignments = [];

    // Parameterless constructor for EF Core materialization.
    private CollectionPlan() : base(Guid.Empty)
    {
    }

    private CollectionPlan(Guid id, Guid companyId, Guid projectId, Guid batchId) : base(id)
    {
        CompanyId = companyId;
        ProjectId = projectId;
        BatchId = batchId;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>The in vivo project this plan belongs to, referenced by value.</summary>
    public Guid ProjectId { get; private set; }

    /// <summary>The batch (leva) this plan governs, referenced by value (one plan per batch).</summary>
    public Guid BatchId { get; private set; }

    /// <summary>The sample→analysis→storage matrix rows.</summary>
    public IReadOnlyList<SampleRouting> Routings => _routings.AsReadOnly();

    /// <summary>The role→member assignments of the collection sheet.</summary>
    public IReadOnlyList<CollectionRoleAssignment> Assignments => _assignments.AsReadOnly();

    /// <summary>Creates an empty collection plan for a batch and raises the created event.</summary>
    public static CollectionPlan Create(Guid companyId, Guid projectId, Guid batchId)
    {
        Guard.AgainstEmptyGuid(companyId, nameof(companyId));
        Guard.AgainstEmptyGuid(projectId, nameof(projectId));
        Guard.AgainstEmptyGuid(batchId, nameof(batchId));

        var plan = new CollectionPlan(Guid.NewGuid(), companyId, projectId, batchId);
        plan.RaiseDomainEvent(new CollectionPlanCreatedEvent(companyId, plan.Id, projectId, batchId));
        return plan;
    }

    /// <summary>
    /// Defines (or replaces) the routing for a sample type: its planned analyses and storage. Idempotent by sample type
    /// — re-defining an existing type's routing overwrites it rather than adding a second row, so the matrix keeps one
    /// row per sample type.
    /// </summary>
    public void DefineRouting(
        SampleType sampleType,
        IEnumerable<string> plannedAnalyses,
        Guid? storageRoomId = null,
        string? storageLabel = null,
        TemperatureRange? conservationRange = null)
    {
        SampleRouting? existing = _routings.FirstOrDefault(routing => routing.SampleType == sampleType);
        if (existing is null)
        {
            _routings.Add(SampleRouting.For(sampleType, plannedAnalyses, storageRoomId, storageLabel, conservationRange));
            return;
        }

        existing.ReplacePlannedAnalyses(plannedAnalyses);
        existing.ChangeStorage(storageRoomId, storageLabel, conservationRange);
    }

    /// <summary>Removes the routing for a sample type. It is an error to remove a type the matrix does not route.</summary>
    public void RemoveRouting(SampleType sampleType)
    {
        SampleRouting routing = _routings.FirstOrDefault(r => r.SampleType == sampleType)
            ?? throw new NotFoundException($"The plan has no routing for sample type '{sampleType}'.");

        _routings.Remove(routing);
    }

    /// <summary>The routing for <paramref name="sampleType"/>, or null when the matrix does not route it.</summary>
    public SampleRouting? RoutingFor(SampleType sampleType)
        => _routings.FirstOrDefault(routing => routing.SampleType == sampleType);

    /// <summary>
    /// Assigns a member to a collection role. Idempotent by role — assigning an already-assigned role reassigns it to
    /// the new member rather than adding a second row, so a role has exactly one person in charge.
    /// </summary>
    public void AssignRole(Guid roleId, Guid userId)
    {
        Guard.AgainstEmptyGuid(roleId, nameof(roleId));
        Guard.AgainstEmptyGuid(userId, nameof(userId));

        CollectionRoleAssignment? existing = _assignments.FirstOrDefault(assignment => assignment.IsForRole(roleId));
        if (existing is null)
            _assignments.Add(CollectionRoleAssignment.Of(roleId, userId));
        else
            existing.ReassignTo(userId);
    }

    /// <summary>Removes a role assignment. It is an error to remove a role the plan has not assigned.</summary>
    public void RemoveAssignment(Guid roleId)
    {
        CollectionRoleAssignment assignment = _assignments.FirstOrDefault(a => a.IsForRole(roleId))
            ?? throw new NotFoundException($"The plan has no assignment for role '{roleId}'.");

        _assignments.Remove(assignment);
    }
}
