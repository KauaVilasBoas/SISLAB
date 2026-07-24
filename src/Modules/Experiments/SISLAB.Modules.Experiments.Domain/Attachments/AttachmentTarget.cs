using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Attachments;

/// <summary>
/// The reading/analysis a piece of evidence documents (SISLAB-09): a <see cref="Kind"/> plus the id of the target it
/// points at (<see cref="TargetId"/>) — a biobank sample analysis or an experiment reading. The pairing is what lets a
/// query answer "which files document <i>this</i> analysis" without the attachment owning the target aggregate; the
/// target is referenced only by value (module isolation, no cross-aggregate navigation), exactly like the rest of the
/// module.
/// </summary>
/// <remarks>
/// Immutable value object with structural equality: two targets are equal when they share the same kind and id. An
/// attachment additionally carries the owning <c>AnimalId</c> on itself (every evidence hangs off an animal), so the
/// target here is the finer-grained reading, not the animal.
/// </remarks>
public sealed class AttachmentTarget : ValueObject
{
    private AttachmentTarget(AttachmentTargetKind kind, Guid targetId)
    {
        Kind = kind;
        TargetId = targetId;
    }

    /// <summary>Whether the evidence documents a sample analysis or an experiment reading.</summary>
    public AttachmentTargetKind Kind { get; }

    /// <summary>The id of the analysis/reading the evidence documents, referenced by value.</summary>
    public Guid TargetId { get; }

    /// <summary>The evidence documents the biobank sample analysis with <paramref name="analysisId"/>.</summary>
    public static AttachmentTarget ForSampleAnalysis(Guid analysisId)
    {
        Guard.AgainstEmptyGuid(analysisId, nameof(analysisId));
        return new AttachmentTarget(AttachmentTargetKind.SampleAnalysis, analysisId);
    }

    /// <summary>The evidence documents the experiment reading with <paramref name="readingId"/>.</summary>
    public static AttachmentTarget ForExperimentReading(Guid readingId)
    {
        Guard.AgainstEmptyGuid(readingId, nameof(readingId));
        return new AttachmentTarget(AttachmentTargetKind.ExperimentReading, readingId);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return TargetId;
    }
}
