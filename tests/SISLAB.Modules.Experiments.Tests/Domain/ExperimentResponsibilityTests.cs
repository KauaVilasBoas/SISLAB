using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Tests.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain;

/// <summary>
/// Domain tests for the responsibility model + edit-authorization invariant (card [E11]): the lead responsible,
/// per-step responsibles, and who may edit. The authorization matrix (lead / step responsible / unrelated third
/// party) is asserted here on the aggregate, where the rule lives.
/// </summary>
public sealed class ExperimentResponsibilityTests
{
    private static readonly Guid Lead = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid StepPerson = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Outsider = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    [Fact]
    public void A_fresh_experiment_has_no_responsibility_configured()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();

        Assert.Null(experiment.ResponsibleUserId);
        Assert.False(experiment.HasResponsibilityConfigured);
    }

    [Fact]
    public void With_no_responsibility_configured_anyone_may_edit_backward_compatibility()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();

        Assert.True(experiment.CanBeEditedBy(Outsider));
        experiment.EnsureCanBeEditedBy(Outsider); // does not throw
    }

    [Fact]
    public void AssignResponsible_sets_the_lead_and_activates_the_gate()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();

        experiment.AssignResponsible(Lead);

        Assert.Equal(Lead, experiment.ResponsibleUserId);
        Assert.True(experiment.HasResponsibilityConfigured);
    }

    [Fact]
    public void AssignResponsible_rejects_an_empty_user()
        => Assert.Throws<DomainException>(() => ExperimentTestData.NewExperiment().AssignResponsible(Guid.Empty));

    [Fact]
    public void The_lead_may_edit_anything()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.AssignResponsible(Lead);

        Guid anyStepId = experiment.Steps[0].Id;

        Assert.True(experiment.CanBeEditedBy(Lead));
        Assert.True(experiment.CanBeEditedBy(Lead, anyStepId));
    }

    [Fact]
    public void A_step_responsible_may_edit_only_their_step()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.AssignResponsible(Lead);

        Guid theirStep = experiment.Steps[0].Id;
        Guid otherStep = experiment.Steps[1].Id;
        experiment.AssignStepResponsible(theirStep, StepPerson);

        Assert.True(experiment.CanBeEditedBy(StepPerson, theirStep));
        Assert.False(experiment.CanBeEditedBy(StepPerson, otherStep));
        // Experiment-wide edit (no step) is lead-only.
        Assert.False(experiment.CanBeEditedBy(StepPerson));
    }

    [Fact]
    public void An_unrelated_user_may_not_edit_once_responsibility_is_configured()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.AssignResponsible(Lead);
        Guid step = experiment.Steps[0].Id;

        Assert.False(experiment.CanBeEditedBy(Outsider));
        Assert.False(experiment.CanBeEditedBy(Outsider, step));
        Assert.Throws<ForbiddenException>(() => experiment.EnsureCanBeEditedBy(Outsider));
        Assert.Throws<ForbiddenException>(() => experiment.EnsureCanBeEditedBy(Outsider, step));
    }

    [Fact]
    public void AssignStepResponsible_is_idempotent_and_a_step_may_have_many()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        Guid step = experiment.Steps[0].Id;

        experiment.AssignStepResponsible(step, StepPerson);
        experiment.AssignStepResponsible(step, StepPerson); // idempotent
        experiment.AssignStepResponsible(step, Outsider);

        IReadOnlyCollection<Guid> responsibles = experiment.Steps[0].ResponsibleUserIds;
        Assert.Equal(2, responsibles.Count);
        Assert.Contains(StepPerson, responsibles);
        Assert.Contains(Outsider, responsibles);
    }

    [Fact]
    public void RemoveStepResponsible_removes_the_user_and_the_gate_falls_dormant_when_nothing_remains()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        Guid step = experiment.Steps[0].Id;
        experiment.AssignStepResponsible(step, StepPerson);

        Assert.True(experiment.HasResponsibilityConfigured);

        experiment.RemoveStepResponsible(step, StepPerson);

        Assert.False(experiment.Steps[0].IsResponsible(StepPerson));
        Assert.False(experiment.HasResponsibilityConfigured);
    }

    [Fact]
    public void AssignStepResponsible_rejects_a_step_that_is_not_in_the_experiment()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();

        Assert.Throws<DomainException>(
            () => experiment.AssignStepResponsible(Guid.NewGuid(), StepPerson));
    }
}
