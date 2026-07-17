using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Domain.Projects.Events;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain;

public sealed class ProjectTests
{
    private static Project NewProject()
        => Project.Create("Neuropatia — composto X", "Rattus norvegicus (Wistar)");

    [Fact]
    public void Create_starts_in_draft_at_version_one_and_raises_created()
    {
        Project project = NewProject();

        Assert.Equal(ProjectStatus.Draft, project.Status);
        Assert.Equal(1, project.CurrentDesignVersion);
        Assert.Empty(project.Batches);
        Assert.Contains(project.DomainEvents, e => e is ProjectCreatedEvent);
    }

    [Fact]
    public void AddBatch_pins_the_current_design_version()
    {
        Project project = NewProject();

        Batch batch = project.AddBatch("Leva 1");

        Assert.Equal(1, batch.DesignVersion);
        Assert.Equal(BatchStatus.Planned, batch.Status);
        Assert.Single(project.Batches);
    }

    [Fact]
    public void ReviseDesign_bumps_version_so_new_batches_run_the_revised_design_and_old_ones_do_not()
    {
        Project project = NewProject();
        Batch first = project.AddBatch("Leva 1");

        int revised = project.ReviseDesign();
        Batch second = project.AddBatch("Leva 2");

        Assert.Equal(2, revised);
        Assert.Equal(1, first.DesignVersion);
        Assert.Equal(2, second.DesignVersion);
    }

    [Fact]
    public void AddAnimal_keeps_the_identifier_unique_across_the_whole_project()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group control = project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));
        Group dose = project.AddGroup(batch.Id, "Dose 10", Dose.Of(10m, "mg/kg"));

        project.AddAnimal(batch.Id, control.Id, "M1-01", AnimalSex.Male);

        Assert.Throws<ConflictException>(() =>
            project.AddAnimal(batch.Id, dose.Id, "M1-01", AnimalSex.Male));
    }

    [Fact]
    public void StartBatch_freezes_the_design_activates_the_project_and_raises_the_event()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group control = project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));
        project.AddAnimal(batch.Id, control.Id, "M1-01", AnimalSex.Male);

        project.StartBatch(batch.Id);

        Assert.Equal(BatchStatus.Running, project.FindBatch(batch.Id).Status);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Contains(project.DomainEvents, e => e is BatchStartedEvent);
    }

    [Fact]
    public void A_started_batch_is_frozen_and_rejects_further_design_edits()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group control = project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));
        project.AddAnimal(batch.Id, control.Id, "M1-01", AnimalSex.Male);
        project.StartBatch(batch.Id);

        Assert.Throws<DomainException>(() => project.AddGroup(batch.Id, "Dose 10", Dose.Of(10m, "mg/kg")));
        Assert.Throws<DomainException>(() =>
            project.AddAnimal(batch.Id, control.Id, "M1-02", AnimalSex.Female));
    }

    [Fact]
    public void StartBatch_requires_at_least_one_animal()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));

        Assert.Throws<DomainException>(() => project.StartBatch(batch.Id));
    }

    [Fact]
    public void Close_is_rejected_while_a_batch_is_still_running()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group control = project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));
        project.AddAnimal(batch.Id, control.Id, "M1-01", AnimalSex.Male);
        project.StartBatch(batch.Id);

        Assert.Throws<DomainException>(() => project.Close());

        project.CompleteBatch(batch.Id);
        project.Close();
        Assert.Equal(ProjectStatus.Closed, project.Status);
    }

    [Fact]
    public void Dose_is_compared_by_value_and_guards_a_non_negative_amount()
    {
        Assert.Equal(Dose.Of(10m, "mg/kg"), Dose.Of(10m, "mg/kg"));
        Assert.NotEqual(Dose.Of(10m, "mg/kg"), Dose.Of(10m, "µg"));
        Assert.Throws<DomainException>(() => Dose.Of(-1m, "mg/kg"));
    }
}
