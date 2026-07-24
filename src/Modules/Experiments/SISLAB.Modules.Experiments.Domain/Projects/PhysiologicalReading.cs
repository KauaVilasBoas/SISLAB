using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A single recurring physiological reading of one animal at one timepoint (SISLAB-02): a numeric value for a named
/// parameter (e.g. glicemia 268 mg/dL, peso 189.6 g) captured at a labelled timepoint (basal, pós-indução, 7/15/21/28
/// dias), authored by <see cref="RecordedBy"/> at <see cref="RecordedAtUtc"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a dedicated reading, not a <c>BehavioralMeasurement</c>.</b> Behavioural measurements live on the separate
/// <c>Experiment</c> aggregate and keep a free-text raw the assay's versioned Strategy interprets. Physiological
/// readings are structured numeric values kept <i>on the design</i> (the <see cref="Project"/> aggregate), because
/// the animal-selection criterion (SISLAB-02) evaluates them directly — glicemia ≥ limiar drives inclusion/exclusion,
/// which mutates the very <see cref="Animal"/> these readings hang off. Keeping both in one aggregate lets the
/// selection stay a single-aggregate invariant.
/// </para>
/// <para>
/// <b>Parameter by code, extensible.</b> The <see cref="ParameterCode"/> is free text (normalized), so glicemia/peso
/// are just the current lab's codes — never an enum constant — and a new parameter needs no code change. The
/// <see cref="AnimalId"/> references a <see cref="Animal"/> <b>by value</b> (the animal is reached through the same
/// aggregate, but the reading holds the id, not a navigation), honouring the module's ids-by-value rule.
/// </para>
/// </remarks>
public sealed class PhysiologicalReading : Entity<Guid>
{
    internal const int MaxParameterCodeLength = 60;
    internal const int MaxUnitLength = 30;
    private const int MaxTimepointLabelLength = 60;
    private const int MaxRecordedByLength = 200;

    // Parameterless constructor for EF Core materialization.
    private PhysiologicalReading() : base(Guid.Empty)
    {
        ParameterCode = default!;
        Unit = default!;
        TimepointLabel = default!;
        RecordedBy = default!;
    }

    private PhysiologicalReading(
        Guid id,
        Guid animalId,
        string parameterCode,
        decimal value,
        string unit,
        string timepointLabel,
        string recordedBy,
        DateTime recordedAtUtc)
        : base(id)
    {
        AnimalId = animalId;
        ParameterCode = parameterCode;
        Value = value;
        Unit = unit;
        TimepointLabel = timepointLabel;
        RecordedBy = recordedBy;
        RecordedAtUtc = recordedAtUtc;
    }

    /// <summary>The animal this reading was taken on, referenced by value (a project animal id).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>The measured parameter's code (normalized, case-insensitive), e.g. "glicemia", "peso".</summary>
    public string ParameterCode { get; private set; }

    /// <summary>The reading's numeric value (e.g. 268 for a glicemia of 268 mg/dL).</summary>
    public decimal Value { get; private set; }

    /// <summary>The unit the value is expressed in (e.g. "mg/dL", "g").</summary>
    public string Unit { get; private set; }

    /// <summary>The timepoint the reading was taken at (e.g. "basal", "pós-indução", "28 dias").</summary>
    public string TimepointLabel { get; private set; }

    /// <summary>Operator who recorded the reading (identity claim).</summary>
    public string RecordedBy { get; private set; }

    /// <summary>Instant (UTC) the reading was recorded.</summary>
    public DateTime RecordedAtUtc { get; private set; }

    /// <summary>Whether this reading is for <paramref name="parameterCode"/> (case-insensitively).</summary>
    public bool IsForParameter(string parameterCode)
        => !string.IsNullOrWhiteSpace(parameterCode)
           && string.Equals(ParameterCode, parameterCode.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a reading, guarding a real animal, a present parameter code/unit/timepoint/author. The value is kept
    /// as-is (physiological readings may be zero or, defensively, negative for a mis-entered delta) — validity of the
    /// magnitude is the caller's/UI's concern, not an aggregate invariant.
    /// </summary>
    public static PhysiologicalReading Create(
        Guid animalId,
        string parameterCode,
        decimal value,
        string unit,
        string timepointLabel,
        string recordedBy,
        DateTime recordedAtUtc)
    {
        Guard.AgainstEmptyGuid(animalId, nameof(animalId));

        return new PhysiologicalReading(
            Guid.NewGuid(),
            animalId,
            Normalize(parameterCode, MaxParameterCodeLength, nameof(parameterCode)),
            value,
            Normalize(unit, MaxUnitLength, nameof(unit)),
            Normalize(timepointLabel, MaxTimepointLabelLength, nameof(timepointLabel)),
            Normalize(recordedBy, MaxRecordedByLength, nameof(recordedBy)),
            recordedAtUtc);
    }

    private static string Normalize(string value, int maxLength, string parameterName)
    {
        Guard.AgainstNullOrWhiteSpace(value, parameterName);
        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
