using SISLAB.Modules.Experiments.Application.Biobank.Commands;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

public sealed class SampleCommandHandlerTests
{
    private static readonly DateTime When = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    private static VonFreiExperiment NewExperiment()
        => VonFreiExperiment.Create(
            "Von Frey", null, "tester@lab", When, Guid.NewGuid(), Guid.NewGuid(), new[] { "Baseline", "T30" });

    [Fact]
    public async Task Collect_records_the_collection_step_and_persists_the_sample_from_the_experiment_ids()
    {
        VonFreiExperiment experiment = NewExperiment();
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var samples = new FakeSampleRepository();
        var handler = new CollectSampleCommandHandler(
            samples, experiments, new FakeActorAccessor("tech@lab"), new FixedClock(When));

        Guid id = await handler.HandleAsync(new CollectSampleCommand(
            experiment.Id, Guid.NewGuid(), "S-001", SampleType.Plasma, 2.0m, "mL",
            ConservationTempMinCelsius: -80m, ConservationTempMaxCelsius: -20m,
            StorageLabel: "Freezer A / Box 3", Notes: null));

        Sample created = Assert.IsType<Sample>(samples.LastAdded);
        Assert.Equal(id, created.Id);
        // Origin ids come from the experiment, not the payload.
        Assert.Equal(experiment.ProjectId, created.ProjectId);
        Assert.Equal(experiment.BatchId, created.BatchId);
        Assert.Equal(experiment.Id, created.SourceExperimentId);
        Assert.Equal(2.0m, created.RemainingQuantity.Value);
        Assert.NotNull(created.ConservationRange);
        // The collection hand-off is recorded on the experiment's flow.
        Assert.NotNull(experiments.LastUpdated);
        Assert.Contains(
            experiments.LastUpdated!.Steps,
            step => step.Kind == ExperimentStepKind.Collection && step.IsPerformed);
    }

    [Fact]
    public async Task Collect_rejects_a_duplicate_code()
    {
        VonFreiExperiment experiment = NewExperiment();
        var experiments = new FakeExperimentRepository().Seed(experiment);
        Sample existing = Sample.Collect(
            "S-001", SampleType.Blood, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SampleAmount.Of(1m, "mL"), "tech@lab", When);
        var samples = new FakeSampleRepository().Seed(existing);
        var handler = new CollectSampleCommandHandler(
            samples, experiments, new FakeActorAccessor(), new FixedClock(When));

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new CollectSampleCommand(
            experiment.Id, Guid.NewGuid(), "S-001", SampleType.Plasma, 2.0m, "mL",
            null, null, null, null)));
    }

    [Fact]
    public async Task Collect_on_a_missing_experiment_throws_not_found()
    {
        var handler = new CollectSampleCommandHandler(
            new FakeSampleRepository(), new FakeExperimentRepository(),
            new FakeActorAccessor(), new FixedClock(When));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new CollectSampleCommand(
            Guid.NewGuid(), Guid.NewGuid(), "S-001", SampleType.Plasma, 2.0m, "mL",
            null, null, null, null)));
    }

    [Fact]
    public async Task Analyse_consumes_the_balance_and_persists()
    {
        Sample sample = Sample.Collect(
            "S-001", SampleType.Plasma, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SampleAmount.Of(2m, "mL"), "tech@lab", When);
        var samples = new FakeSampleRepository().Seed(sample);
        var handler = new AnalyseSampleCommandHandler(samples, new FakeActorAccessor(), new FixedClock(When));

        Guid analysisId = await handler.HandleAsync(new AnalyseSampleCommand(sample.Id, "ELISA", 0.5m, "mL"));

        Assert.NotNull(samples.LastUpdated);
        Assert.Equal(1.5m, samples.LastUpdated!.RemainingQuantity.Value);
        Assert.Contains(samples.LastUpdated.Analyses, a => a.Id == analysisId);
    }

    [Fact]
    public async Task RecordResult_completes_the_analysis()
    {
        Sample sample = Sample.Collect(
            "S-001", SampleType.Plasma, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SampleAmount.Of(2m, "mL"), "tech@lab", When);
        Analysis analysis = sample.Analyse("ELISA", SampleAmount.Of(0.5m, "mL"), "tech@lab", When);
        var samples = new FakeSampleRepository().Seed(sample);
        var handler = new RecordAnalysisResultCommandHandler(samples);

        await handler.HandleAsync(new RecordAnalysisResultCommand(sample.Id, analysis.Id, "42.7 pg/mL"));

        Assert.NotNull(samples.LastUpdated);
        Assert.Equal(AnalysisStatus.Completed, samples.LastUpdated!.Analyses[0].Status);
    }
}
