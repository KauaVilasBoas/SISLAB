using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.Modules.Experiments.Domain.Attachments.Events;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Modules.Experiments.Tests.Domain;

/// <summary>
/// Invariants of the <see cref="Attachment"/> aggregate (SISLAB-09): it keeps only the opaque storage key plus its
/// metadata, links an animal to a reading/analysis by value, treats the origin as an optional label, and raises the
/// registration event.
/// </summary>
public sealed class AttachmentTests
{
    private static readonly DateTime When = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    private static Attachment Register(Guid company, Guid animal, AttachmentTarget target, string? origin = "Fiocruz")
        => Attachment.Register(
            company, animal, target, StoredFileKey.Of("obj-key"),
            "laudo.pdf", "application/pdf", sizeBytes: 42, origin, "tech@lab", When);

    [Fact]
    public void Register_keeps_the_key_metadata_and_link_and_raises_the_event()
    {
        Guid company = Guid.NewGuid();
        Guid animal = Guid.NewGuid();
        Guid analysisId = Guid.NewGuid();

        Attachment attachment = Register(company, animal, AttachmentTarget.ForSampleAnalysis(analysisId));

        Assert.Equal(company, attachment.CompanyId);
        Assert.Equal(animal, attachment.AnimalId);
        Assert.Equal("obj-key", attachment.StorageKey.Value);
        Assert.Equal(AttachmentTargetKind.SampleAnalysis, attachment.Target.Kind);
        Assert.Equal(analysisId, attachment.Target.TargetId);
        Assert.Equal("Fiocruz", attachment.Origin);
        Assert.Contains(attachment.DomainEvents, e => e is AttachmentRegisteredEvent);
    }

    [Fact]
    public void Register_treats_a_blank_origin_as_absent()
    {
        Attachment attachment = Register(
            Guid.NewGuid(), Guid.NewGuid(), AttachmentTarget.ForExperimentReading(Guid.NewGuid()), origin: "   ");

        Assert.Null(attachment.Origin);
    }

    [Fact]
    public void Register_rejects_an_empty_animal()
    {
        Assert.Throws<DomainException>(() =>
            Register(Guid.NewGuid(), Guid.Empty, AttachmentTarget.ForSampleAnalysis(Guid.NewGuid())));
    }

    [Fact]
    public void Register_rejects_a_negative_size()
    {
        Assert.Throws<DomainException>(() => Attachment.Register(
            Guid.NewGuid(), Guid.NewGuid(), AttachmentTarget.ForSampleAnalysis(Guid.NewGuid()),
            StoredFileKey.Of("k"), "f.pdf", "application/pdf", sizeBytes: -1, null, "tech@lab", When));
    }

    [Fact]
    public void Target_rejects_an_empty_id()
    {
        Assert.Throws<DomainException>(() => AttachmentTarget.ForSampleAnalysis(Guid.Empty));
        Assert.Throws<DomainException>(() => AttachmentTarget.ForExperimentReading(Guid.Empty));
    }

    [Fact]
    public void Target_has_structural_equality()
    {
        Guid id = Guid.NewGuid();

        Assert.Equal(AttachmentTarget.ForSampleAnalysis(id), AttachmentTarget.ForSampleAnalysis(id));
        Assert.NotEqual(AttachmentTarget.ForSampleAnalysis(id), AttachmentTarget.ForExperimentReading(id));
    }
}
