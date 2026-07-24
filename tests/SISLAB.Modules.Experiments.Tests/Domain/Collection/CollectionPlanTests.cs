using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.Modules.Experiments.Domain.Collection.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain.Collection;

/// <summary>
/// Covers the <see cref="CollectionPlan"/> aggregate (SISLAB-08): the sample→analysis→storage matrix and the role
/// roster, with their invariants (one routing per sample type, one member per role, at least one planned analysis) and
/// the idempotent-by-key define/assign semantics. Nothing lab-specific is a constant — every value is an input.
/// </summary>
public sealed class CollectionPlanTests
{
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Project = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Batch = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static CollectionPlan NewPlan() => CollectionPlan.Create(Company, Project, Batch);

    [Fact]
    public void Create_raises_the_created_event_and_starts_empty()
    {
        CollectionPlan plan = NewPlan();

        Assert.Equal(Company, plan.CompanyId);
        Assert.Equal(Batch, plan.BatchId);
        Assert.Empty(plan.Routings);
        Assert.Empty(plan.Assignments);
        Assert.Single(plan.DomainEvents.OfType<CollectionPlanCreatedEvent>());
    }

    [Fact]
    public void DefineRouting_adds_a_matrix_row_with_planned_analyses_and_storage()
    {
        CollectionPlan plan = NewPlan();
        Guid room = Guid.NewGuid();

        plan.DefineRouting(
            SampleType.Blood,
            ["Hemograma", "Bioquímica"],
            room,
            "−20 °C",
            TemperatureRange.Between(-25m, -18m));

        SampleRouting routing = Assert.Single(plan.Routings);
        Assert.Equal(SampleType.Blood, routing.SampleType);
        Assert.Equal(room, routing.StorageRoomId);
        Assert.Equal("−20 °C", routing.StorageLabel);
        Assert.Equal(2, routing.PlannedAnalyses.Count);
        Assert.True(routing.Plans("hemograma"));
    }

    [Fact]
    public void DefineRouting_is_idempotent_by_sample_type_overwriting_the_row()
    {
        CollectionPlan plan = NewPlan();

        plan.DefineRouting(SampleType.Tissue, ["PCR"]);
        plan.DefineRouting(SampleType.Tissue, ["MDA", "Nitrito"], storageLabel: "s/ processar");

        SampleRouting routing = Assert.Single(plan.Routings);
        Assert.False(routing.Plans("PCR"));
        Assert.True(routing.Plans("MDA"));
        Assert.Equal("s/ processar", routing.StorageLabel);
    }

    [Fact]
    public void DefineRouting_collapses_duplicate_analysis_names()
    {
        CollectionPlan plan = NewPlan();

        plan.DefineRouting(SampleType.Plasma, ["ELISA", "elisa", " ELISA "]);

        SampleRouting routing = Assert.Single(plan.Routings);
        Assert.Single(routing.PlannedAnalyses);
    }

    [Fact]
    public void DefineRouting_requires_at_least_one_planned_analysis()
    {
        CollectionPlan plan = NewPlan();

        Assert.Throws<DomainException>(() => plan.DefineRouting(SampleType.Blood, []));
    }

    [Fact]
    public void RemoveRouting_removes_the_row_and_errors_on_an_absent_type()
    {
        CollectionPlan plan = NewPlan();
        plan.DefineRouting(SampleType.Blood, ["Hemograma"]);

        plan.RemoveRouting(SampleType.Blood);
        Assert.Empty(plan.Routings);

        Assert.Throws<NotFoundException>(() => plan.RemoveRouting(SampleType.Blood));
    }

    [Fact]
    public void AssignRole_adds_an_assignment_and_is_idempotent_by_role_reassigning_the_member()
    {
        CollectionPlan plan = NewPlan();
        Guid role = Guid.NewGuid();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        plan.AssignRole(role, first);
        plan.AssignRole(role, second);

        CollectionRoleAssignment assignment = Assert.Single(plan.Assignments);
        Assert.Equal(role, assignment.RoleId);
        Assert.Equal(second, assignment.UserId);
    }

    [Fact]
    public void RemoveAssignment_removes_the_role_and_errors_on_an_absent_role()
    {
        CollectionPlan plan = NewPlan();
        Guid role = Guid.NewGuid();
        plan.AssignRole(role, Guid.NewGuid());

        plan.RemoveAssignment(role);
        Assert.Empty(plan.Assignments);

        Assert.Throws<NotFoundException>(() => plan.RemoveAssignment(role));
    }
}
