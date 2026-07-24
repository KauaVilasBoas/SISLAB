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
        Cage cx1 = project.AddCage(batch.Id, "CX1", capacity: 4);
        Cage cx2 = project.AddCage(batch.Id, "CX2", capacity: 4);

        project.AddAnimalToCage(batch.Id, cx1.Id, "M1-01", AnimalSex.Male);

        Assert.Throws<ConflictException>(() =>
            project.AddAnimalToCage(batch.Id, cx2.Id, "M1-01", AnimalSex.Male));
    }

    [Fact]
    public void An_animal_can_exist_in_a_cage_without_a_group_assigned()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);

        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male, weightGrams: 189.6m);

        Assert.Null(animal.GroupId);
        Assert.Single(project.FindBatch(batch.Id).Cages);
        Assert.Equal(1, project.FindBatch(batch.Id).AnimalCount);
    }

    [Fact]
    public void A_cage_rejects_an_animal_beyond_its_capacity()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 2);
        project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A2", AnimalSex.Female);

        Assert.Throws<DomainException>(() =>
            project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A3", AnimalSex.Male));
    }

    [Fact]
    public void Cage_capacity_is_a_parameter_not_fixed_at_four()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 6);

        for (int i = 1; i <= 6; i++)
            project.AddAnimalToCage(batch.Id, cage.Id, $"CX1-A{i}", AnimalSex.Male);

        Assert.Equal(6, project.FindBatch(batch.Id).AnimalCount);
    }

    [Fact]
    public void AssignAnimalToGroup_moves_an_unassigned_animal_into_a_group_after_basal()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group dose = project.AddGroup(batch.Id, "Dose 3", Dose.Of(3m, "g/kg"));
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);

        project.AssignAnimalToGroup(batch.Id, animal.Id, dose.Id);

        Assert.Equal(dose.Id, animal.GroupId);
    }

    [Fact]
    public void AssignAnimalToGroup_redistributes_a_discrepant_cage_across_groups()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group control = project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));
        Group dose = project.AddGroup(batch.Id, "Dose 3", Dose.Of(3m, "g/kg"));
        Cage cage = project.AddCage(batch.Id, "CX7", capacity: 4);
        // The whole cage first goes to control, then two animals are moved out to the dose group (redistribution).
        Animal a1 = project.AddAnimalToCage(batch.Id, cage.Id, "CX7-A1", AnimalSex.Male, groupId: control.Id);
        Animal a2 = project.AddAnimalToCage(batch.Id, cage.Id, "CX7-A2", AnimalSex.Male, groupId: control.Id);

        project.AssignAnimalToGroup(batch.Id, a2.Id, dose.Id);

        Assert.Equal(control.Id, a1.GroupId);
        Assert.Equal(dose.Id, a2.GroupId);
    }

    [Fact]
    public void AssignAnimalToGroup_rejects_a_group_from_another_batch()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);

        Assert.Throws<NotFoundException>(() =>
            project.AssignAnimalToGroup(batch.Id, animal.Id, Guid.NewGuid()));
    }

    [Fact]
    public void AddAnimalToCage_can_assign_the_group_at_entry_for_the_pre_induction_flow()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group dose = project.AddGroup(batch.Id, "Dose 3", Dose.Of(3m, "g/kg"));
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);

        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male, groupId: dose.Id);

        Assert.Equal(dose.Id, animal.GroupId);
    }

    [Fact]
    public void StartBatch_freezes_the_design_activates_the_project_and_raises_the_event()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);

        project.StartBatch(batch.Id);

        Assert.Equal(BatchStatus.Running, project.FindBatch(batch.Id).Status);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Contains(project.DomainEvents, e => e is BatchStartedEvent);
    }

    [Fact]
    public void A_started_batch_freezes_randomization_and_rejects_further_design_edits()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Group control = project.AddGroup(batch.Id, "Controle", Dose.Of(0m, "mg/kg"));
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        project.StartBatch(batch.Id);

        Assert.Throws<DomainException>(() => project.AddGroup(batch.Id, "Dose 10", Dose.Of(10m, "mg/kg")));
        Assert.Throws<DomainException>(() => project.AddCage(batch.Id, "CX2", capacity: 4));
        Assert.Throws<DomainException>(() =>
            project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A2", AnimalSex.Female));
        // Randomization/assignment is locked once the leva starts.
        Assert.Throws<DomainException>(() => project.AssignAnimalToGroup(batch.Id, animal.Id, control.Id));
    }

    [Fact]
    public void StartBatch_requires_at_least_one_animal()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        project.AddCage(batch.Id, "CX1", capacity: 4);

        Assert.Throws<DomainException>(() => project.StartBatch(batch.Id));
    }

    [Fact]
    public void Close_is_rejected_while_a_batch_is_still_running()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        project.StartBatch(batch.Id);

        Assert.Throws<DomainException>(() => project.Close());

        project.CompleteBatch(batch.Id);
        project.Close();
        Assert.Equal(ProjectStatus.Closed, project.Status);
    }

    [Fact]
    public void BindBatchToModel_stores_the_model_id_by_value_while_the_design_is_open()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        var modelId = Guid.NewGuid();

        project.BindBatchToModel(batch.Id, modelId);

        Assert.Equal(modelId, project.FindBatch(batch.Id).ExperimentalModelId);
    }

    [Fact]
    public void BindBatchToModel_can_rebind_to_another_model_while_planned()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        project.BindBatchToModel(batch.Id, Guid.NewGuid());
        var newModelId = Guid.NewGuid();

        project.BindBatchToModel(batch.Id, newModelId);

        Assert.Equal(newModelId, project.FindBatch(batch.Id).ExperimentalModelId);
    }

    [Fact]
    public void BindBatchToModel_rejects_an_empty_model_id()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");

        Assert.Throws<DomainException>(() => project.BindBatchToModel(batch.Id, Guid.Empty));
    }

    [Fact]
    public void ClearBatchModel_unbinds_the_model_while_planned()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        project.BindBatchToModel(batch.Id, Guid.NewGuid());

        project.ClearBatchModel(batch.Id);

        Assert.Null(project.FindBatch(batch.Id).ExperimentalModelId);
    }

    [Fact]
    public void Binding_the_model_is_frozen_once_the_batch_starts()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        project.BindBatchToModel(batch.Id, Guid.NewGuid());
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        project.StartBatch(batch.Id);

        Assert.Throws<DomainException>(() => project.BindBatchToModel(batch.Id, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => project.ClearBatchModel(batch.Id));
    }

    [Fact]
    public void RecordPhysiologicalReading_stores_the_reading_against_an_enrolled_animal()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);
        var when = new DateTime(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc);

        PhysiologicalReading reading = project.RecordPhysiologicalReading(
            animal.Id, "glicemia", 268m, "mg/dL", "pós-indução", "vic@lab", when);

        PhysiologicalReading stored = Assert.Single(project.PhysiologicalReadings);
        Assert.Equal(reading.Id, stored.Id);
        Assert.Equal(animal.Id, stored.AnimalId);
        Assert.Equal(268m, stored.Value);
        Assert.Equal("mg/dL", stored.Unit);
        Assert.Equal("pós-indução", stored.TimepointLabel);
        Assert.Equal("vic@lab", stored.RecordedBy);
        Assert.Equal(when, stored.RecordedAtUtc);
        Assert.True(stored.IsForParameter("GLICEMIA"));
    }

    [Fact]
    public void RecordPhysiologicalReading_rejects_an_animal_not_enrolled_in_the_project()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);

        Assert.Throws<NotFoundException>(() => project.RecordPhysiologicalReading(
            Guid.NewGuid(), "glicemia", 268m, "mg/dL", "basal", "vic@lab", DateTime.UtcNow));
    }

    [Fact]
    public void RecordPhysiologicalReading_can_capture_a_parameter_at_several_timepoints()
    {
        Project project = NewProject();
        Batch batch = project.AddBatch("Leva 1");
        Cage cage = project.AddCage(batch.Id, "CX1", capacity: 4);
        Animal animal = project.AddAnimalToCage(batch.Id, cage.Id, "CX1-A1", AnimalSex.Male);

        project.RecordPhysiologicalReading(animal.Id, "peso", 189.6m, "g", "basal", "vic@lab", DateTime.UtcNow);
        project.RecordPhysiologicalReading(animal.Id, "peso", 195.2m, "g", "28 dias", "dai@lab", DateTime.UtcNow);

        Assert.Equal(2, project.PhysiologicalReadings.Count(r => r.IsForParameter("peso")));
    }

    [Fact]
    public void Dose_is_compared_by_value_and_guards_a_non_negative_amount()
    {
        Assert.Equal(Dose.Of(10m, "mg/kg"), Dose.Of(10m, "mg/kg"));
        Assert.NotEqual(Dose.Of(10m, "mg/kg"), Dose.Of(10m, "µg"));
        Assert.Throws<DomainException>(() => Dose.Of(-1m, "mg/kg"));
    }
}
