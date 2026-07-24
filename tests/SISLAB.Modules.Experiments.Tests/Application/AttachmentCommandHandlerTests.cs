using System.Text;
using SISLAB.Modules.Experiments.Application.Attachments.Commands;
using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Handler tests for SISLAB-09: the anexo↔animal↔análise association, the round-trip through the storage port, and the
/// tenant stamping. Every dependency is faked (no EF, no disk), so the behaviour under test is the handler's linkage
/// validation and its use of the <see cref="SISLAB.SharedKernel.Storage.IFileStorage"/> port.
/// </summary>
public sealed class AttachmentCommandHandlerTests
{
    private static readonly DateTime When = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Company = Guid.NewGuid();

    private static Stream Bytes(string content = "evidence-bytes")
        => new MemoryStream(Encoding.UTF8.GetBytes(content));

    private static AttachEvidenceCommandHandler NewHandler(
        FakeAttachmentRepository attachments,
        FakeSampleRepository samples,
        FakeExperimentRepository experiments,
        InMemoryFileStorage storage)
        => new(
            attachments,
            samples,
            experiments,
            storage,
            new FakeActorAccessor("tech@lab"),
            new StubTenantContext(Company),
            new FixedClock(When));

    [Fact]
    public async Task Attach_to_a_sample_analysis_persists_the_storage_key_metadata_and_links_the_animal()
    {
        Guid animalId = Guid.NewGuid();
        Sample sample = Sample.Collect(
            Company, "S-001", SampleType.Blood, Guid.NewGuid(), Guid.NewGuid(), animalId, Guid.NewGuid(),
            SampleAmount.Of(2m, "mL"), "tech@lab", When);
        Analysis analysis = sample.Analyse("Hemograma", SampleAmount.Of(0.5m, "mL"), "tech@lab", When);
        var samples = new FakeSampleRepository().Seed(sample);
        var attachments = new FakeAttachmentRepository();
        var storage = new InMemoryFileStorage();
        var handler = NewHandler(attachments, samples, new FakeExperimentRepository(), storage);

        Guid id = await handler.HandleAsync(new AttachEvidenceCommand(
            animalId, AttachmentTargetKind.SampleAnalysis, sample.Id, analysis.Id,
            Bytes(), "hemograma-A1.jpg", "image/jpeg", 13, Origin: "Fiocruz"));

        Attachment created = Assert.IsType<Attachment>(attachments.LastAdded);
        Assert.Equal(id, created.Id);
        // Tenant stamped from the context, not the payload.
        Assert.Equal(Company, created.CompanyId);
        Assert.Equal(animalId, created.AnimalId);
        Assert.Equal(AttachmentTargetKind.SampleAnalysis, created.Target.Kind);
        Assert.Equal(analysis.Id, created.Target.TargetId);
        // Origin is the captured label, never a constant.
        Assert.Equal("Fiocruz", created.Origin);
        Assert.Equal("tech@lab", created.UploadedBy);
        // The domain keeps only the opaque storage key handed back by the port.
        Assert.False(string.IsNullOrWhiteSpace(created.StorageKey.Value));
        Assert.Equal("hemograma-A1.jpg", storage.LastMetadata!.FileName);
    }

    [Fact]
    public async Task Attach_to_an_experiment_reading_links_the_reading_and_animal()
    {
        Guid animalId = Guid.NewGuid();
        HemogramaExperiment experiment = HemogramaExperiment.Create(
            "Hemograma", null, "tech@lab", When, Guid.NewGuid(), Guid.NewGuid(), new[] { "Baseline" });
        experiment.RecordTimepoint("Baseline", new[] { (animalId, "12.4") }, "tech@lab", When);
        Guid readingId = experiment.Measurements.Single().Id;
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var attachments = new FakeAttachmentRepository();
        var handler = NewHandler(attachments, new FakeSampleRepository(), experiments, new InMemoryFileStorage());

        Guid id = await handler.HandleAsync(new AttachEvidenceCommand(
            animalId, AttachmentTargetKind.ExperimentReading, experiment.Id, readingId,
            Bytes(), "leitora-A1.pdf", "application/pdf", 20, Origin: "Leitora externa"));

        Attachment created = Assert.IsType<Attachment>(attachments.LastAdded);
        Assert.Equal(id, created.Id);
        Assert.Equal(AttachmentTargetKind.ExperimentReading, created.Target.Kind);
        Assert.Equal(readingId, created.Target.TargetId);
        Assert.Equal(animalId, created.AnimalId);
    }

    [Fact]
    public async Task Attach_rejects_an_analysis_that_belongs_to_a_different_animal()
    {
        Guid sampleAnimal = Guid.NewGuid();
        Guid otherAnimal = Guid.NewGuid();
        Sample sample = Sample.Collect(
            Company, "S-001", SampleType.Blood, Guid.NewGuid(), Guid.NewGuid(), sampleAnimal, Guid.NewGuid(),
            SampleAmount.Of(2m, "mL"), "tech@lab", When);
        Analysis analysis = sample.Analyse("Hemograma", SampleAmount.Of(0.5m, "mL"), "tech@lab", When);
        var samples = new FakeSampleRepository().Seed(sample);
        var storage = new InMemoryFileStorage();
        var handler = NewHandler(new FakeAttachmentRepository(), samples, new FakeExperimentRepository(), storage);

        // The animal does not own the sample: the link is rejected and no file is spent.
        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new AttachEvidenceCommand(
            otherAnimal, AttachmentTargetKind.SampleAnalysis, sample.Id, analysis.Id,
            Bytes(), "x.jpg", "image/jpeg", 10, Origin: null)));
        Assert.Null(storage.LastMetadata);
    }

    [Fact]
    public async Task Attach_rejects_an_unknown_analysis_on_the_sample()
    {
        Guid animalId = Guid.NewGuid();
        Sample sample = Sample.Collect(
            Company, "S-001", SampleType.Blood, Guid.NewGuid(), Guid.NewGuid(), animalId, Guid.NewGuid(),
            SampleAmount.Of(2m, "mL"), "tech@lab", When);
        var samples = new FakeSampleRepository().Seed(sample);
        var handler = NewHandler(
            new FakeAttachmentRepository(), samples, new FakeExperimentRepository(), new InMemoryFileStorage());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new AttachEvidenceCommand(
            animalId, AttachmentTargetKind.SampleAnalysis, sample.Id, Guid.NewGuid(),
            Bytes(), "x.jpg", "image/jpeg", 10, Origin: null)));
    }

    [Fact]
    public async Task Attach_on_a_missing_sample_throws_not_found()
    {
        var handler = NewHandler(
            new FakeAttachmentRepository(), new FakeSampleRepository(),
            new FakeExperimentRepository(), new InMemoryFileStorage());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new AttachEvidenceCommand(
            Guid.NewGuid(), AttachmentTargetKind.SampleAnalysis, Guid.NewGuid(), Guid.NewGuid(),
            Bytes(), "x.jpg", "image/jpeg", 10, Origin: null)));
    }

    [Fact]
    public async Task Stored_evidence_round_trips_through_the_storage_port()
    {
        Guid animalId = Guid.NewGuid();
        Sample sample = Sample.Collect(
            Company, "S-001", SampleType.Blood, Guid.NewGuid(), Guid.NewGuid(), animalId, Guid.NewGuid(),
            SampleAmount.Of(2m, "mL"), "tech@lab", When);
        Analysis analysis = sample.Analyse("Hemograma", SampleAmount.Of(0.5m, "mL"), "tech@lab", When);
        var samples = new FakeSampleRepository().Seed(sample);
        var attachments = new FakeAttachmentRepository();
        var storage = new InMemoryFileStorage();
        var handler = NewHandler(attachments, samples, new FakeExperimentRepository(), storage);

        await handler.HandleAsync(new AttachEvidenceCommand(
            animalId, AttachmentTargetKind.SampleAnalysis, sample.Id, analysis.Id,
            Bytes("the-real-laudo"), "laudo.txt", "text/plain", 14, Origin: null));

        StoredFileKey key = attachments.LastAdded!.StorageKey;
        await using Stream read = await storage.OpenReadAsync(key);
        using var reader = new StreamReader(read);
        Assert.Equal("the-real-laudo", await reader.ReadToEndAsync());
    }
}
