using SISLAB.Modules.Experiments.Application.Projects.Commands;
using SISLAB.Modules.Experiments.Domain.Preparations;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the <see cref="PrepareGroupSolutionCommandHandler"/> (SISLAB-01): it builds the domain input from the
/// request, lets the aggregate run the pure <see cref="InVivoPreparationCalculator"/> and persists the frozen,
/// traceable <see cref="SolutionPreparation"/> snapshot linked to the batch/group. The spreadsheet case is
/// reproduced end to end (command → aggregate → snapshot), reusing the calculator the domain tests already pin.
/// </summary>
public sealed class PrepareGroupSolutionCommandHandlerTests
{
    private static (Project project, Batch batch, Group group) SeedProjectWithGroup(decimal doseAmount, string doseUnit)
    {
        Project project = Project.Create("Neuropatia diabética", "Rattus norvegicus");
        Batch batch = project.AddBatch("Leva 1");
        Group group = project.AddGroup(batch.Id, "3 g/kg", Dose.Of(doseAmount, doseUnit));
        return (project, batch, group);
    }

    [Fact]
    public async Task Prepare_persists_the_snapshot_reproducing_the_spreadsheet_3g_per_kg_case()
    {
        (Project project, Batch batch, Group group) = SeedProjectWithGroup(3m, "g/kg");
        var projects = new FakeProjectRepository().Seed(project);
        var when = new DateTime(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc);
        var handler = new PrepareGroupSolutionCommandHandler(
            projects, new FakeActorAccessor("vic@lab"), new FixedClock(when));

        // 3 g/kg, group 189.6 g, density 0.9865 g/mL, relation 1 g : 5 µL on a 189 g basis (the spreadsheet's rounding).
        Guid id = await handler.HandleAsync(new PrepareGroupSolutionCommand(
            ProjectId: project.Id,
            BatchId: batch.Id,
            GroupId: group.Id,
            IsVehicleOnly: false,
            RelationMicrolitresPerGram: 5m,
            RelationWeightGrams: 189m,
            DoseAmountGramsPerKilogram: 3m,
            GroupWeightGrams: 189.6m,
            State: CompoundState.Liquid,
            DensityGramsPerMillilitre: 0.9865m));

        SolutionPreparation preparation = Assert.Single(projects.LastUpdated!.SolutionPreparations);
        Assert.Equal(id, preparation.Id);
        Assert.Equal(batch.Id, preparation.BatchId);
        Assert.Equal(group.Id, preparation.GroupId);

        // The frozen result matches the calculator (and therefore the spreadsheet) exactly.
        Assert.Equal(0.5688m, preparation.Result.CompoundMassGrams);
        Assert.Equal(576.58m, preparation.Result.CompoundVolumeMicrolitres);
        Assert.Equal(945m, preparation.Result.FinalVolumeMicrolitres);
        Assert.Equal(368.42m, preparation.Result.DiluentVolumeMicrolitres);

        // Author, instant and formula version are stamped for traceability, never taken from the request body.
        Assert.Equal("vic@lab", preparation.PreparedBy);
        Assert.Equal(when, preparation.PreparedAtUtc);
        Assert.Equal(InVivoPreparationCalculator.FormulaCode, preparation.FormulaCode);
    }

    [Fact]
    public async Task Prepare_is_reproducible_running_the_calculator_over_the_frozen_input_yields_the_same_result()
    {
        (Project project, Batch batch, Group group) = SeedProjectWithGroup(3m, "g/kg");
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new PrepareGroupSolutionCommandHandler(
            projects, new FakeActorAccessor(), new FixedClock(DateTime.UtcNow));

        await handler.HandleAsync(new PrepareGroupSolutionCommand(
            project.Id, batch.Id, group.Id, false, 5m, 189m, 3m, 189.6m, CompoundState.Liquid, 0.9865m));

        SolutionPreparation preparation = Assert.Single(projects.LastUpdated!.SolutionPreparations);

        // Re-running the pure calculator over the snapshot's frozen input reproduces its frozen result — the snapshot
        // is a pure function of its inputs (reproducibility acceptance criterion).
        InVivoPreparationResult recomputed = InVivoPreparationCalculator.Calculate(preparation.Input);
        Assert.Equal(preparation.Result, recomputed);
    }

    [Fact]
    public async Task Prepare_vehicle_only_control_freezes_an_all_diluent_snapshot_without_subtraction()
    {
        (Project project, Batch batch, Group group) = SeedProjectWithGroup(0m, "g/kg");
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new PrepareGroupSolutionCommandHandler(
            projects, new FakeActorAccessor(), new FixedClock(DateTime.UtcNow));

        // Controle: 156 g → 780 µL of vehicle (1 g : 5 µL), no compound, no subtraction.
        await handler.HandleAsync(new PrepareGroupSolutionCommand(
            project.Id, batch.Id, group.Id,
            IsVehicleOnly: true,
            RelationMicrolitresPerGram: 5m,
            RelationWeightGrams: 156m,
            DoseAmountGramsPerKilogram: null,
            GroupWeightGrams: null,
            State: null,
            DensityGramsPerMillilitre: null));

        SolutionPreparation preparation = Assert.Single(projects.LastUpdated!.SolutionPreparations);
        Assert.True(preparation.Input.IsVehicleOnly);
        Assert.Equal(0m, preparation.Result.CompoundMassGrams);
        Assert.Null(preparation.Result.CompoundVolumeMicrolitres);
        Assert.Equal(780m, preparation.Result.FinalVolumeMicrolitres);
        Assert.Equal(780m, preparation.Result.DiluentVolumeMicrolitres);
    }

    [Fact]
    public async Task Prepare_on_a_missing_project_throws_not_found()
    {
        var handler = new PrepareGroupSolutionCommandHandler(
            new FakeProjectRepository(), new FakeActorAccessor(), new FixedClock(DateTime.UtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new PrepareGroupSolutionCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false, 5m, 189m, 3m, 189.6m, CompoundState.Liquid, 0.9865m)));
    }

    [Fact]
    public async Task Prepare_on_a_group_absent_from_the_batch_throws_not_found_and_does_not_persist()
    {
        (Project project, Batch batch, _) = SeedProjectWithGroup(3m, "g/kg");
        var projects = new FakeProjectRepository().Seed(project);
        var handler = new PrepareGroupSolutionCommandHandler(
            projects, new FakeActorAccessor(), new FixedClock(DateTime.UtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new PrepareGroupSolutionCommand(
            project.Id, batch.Id, Guid.NewGuid(), false, 5m, 189m, 3m, 189.6m, CompoundState.Liquid, 0.9865m)));

        Assert.Null(projects.LastUpdated);
    }
}
