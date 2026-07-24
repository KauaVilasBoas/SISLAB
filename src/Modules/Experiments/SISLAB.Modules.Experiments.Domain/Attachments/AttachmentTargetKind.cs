namespace SISLAB.Modules.Experiments.Domain.Attachments;

/// <summary>
/// What a piece of evidence (SISLAB-09) is attached to. An attachment always hangs off a study animal, and in addition
/// it points at the concrete reading/analysis the evidence documents — a biobank sample analysis (e.g. the hemogram
/// laudo the Fiocruz returns for a blood sample) or, more broadly, an experiment reading. Persisted by name, so adding
/// a target later is additive.
/// </summary>
public enum AttachmentTargetKind
{
    /// <summary>The evidence documents a biobank <c>Sample.Analysis</c> (e.g. a hemogram/bioquímica laudo photo).</summary>
    SampleAnalysis = 0,

    /// <summary>The evidence documents an experiment reading (e.g. an external-reader photo tied to a Hemograma run).</summary>
    ExperimentReading = 1,
}
