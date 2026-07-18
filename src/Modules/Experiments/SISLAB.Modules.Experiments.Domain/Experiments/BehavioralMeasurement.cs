using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// A single raw reading of an in vivo behavioural test: one <see cref="AnimalId"/> at one
/// <see cref="TimepointLabel"/> (card [E11] #88). It is the "aplicação (animal × timepoint)" the discovery
/// modelled — captured when an operator launches a timepoint, authored by <see cref="RecordedBy"/> at
/// <see cref="RecordedAtUtc"/>, so the "one person generates the data, another calculates" hand-off is auditable
/// exactly like in vitro.
/// </summary>
/// <remarks>
/// The <see cref="RawValue"/> is kept as a free-text string on purpose so one measurement type serves every
/// behavioural assay: a simple latency test stores a number ("12.4"), while von Frey up-down stores its
/// stimulus/response series ("OXXO..."). The versioned calculation Strategy owns the interpretation of the raw —
/// the domain never parses it. The <see cref="AnimalId"/> references a <see cref="Projects.Animal"/> <b>by value</b>
/// (no cross-aggregate FK/navigation), honouring the module's ids-by-value rule.
/// </remarks>
public sealed class BehavioralMeasurement : Entity<Guid>
{
    private const int MaxTimepointLabelLength = 60;
    private const int MaxRawValueLength = 500;
    private const int MaxRecordedByLength = 200;

    // Parameterless constructor for EF Core materialization.
    private BehavioralMeasurement() : base(Guid.Empty)
    {
        TimepointLabel = default!;
        RawValue = default!;
        RecordedBy = default!;
    }

    private BehavioralMeasurement(
        Guid id,
        Guid animalId,
        string timepointLabel,
        string rawValue,
        string recordedBy,
        DateTime recordedAtUtc)
        : base(id)
    {
        AnimalId = animalId;
        TimepointLabel = timepointLabel;
        RawValue = rawValue;
        RecordedBy = recordedBy;
        RecordedAtUtc = recordedAtUtc;
    }

    /// <summary>The animal this reading was taken on, referenced by value (a project animal id).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>The timepoint at which the reading was taken (e.g. "Baseline", "30 min", "1 h").</summary>
    public string TimepointLabel { get; private set; }

    /// <summary>The raw reading as recorded; interpreted by the assay's versioned calculation Strategy.</summary>
    public string RawValue { get; private set; }

    /// <summary>Operator who launched/recorded this reading (identity claim).</summary>
    public string RecordedBy { get; private set; }

    /// <summary>Instant (UTC) the reading was recorded.</summary>
    public DateTime RecordedAtUtc { get; private set; }

    /// <summary>Creates a measurement, guarding a real animal, a present timepoint label and a present raw value.</summary>
    public static BehavioralMeasurement Create(
        Guid animalId,
        string timepointLabel,
        string rawValue,
        string recordedBy,
        DateTime recordedAtUtc)
    {
        Guard.AgainstEmptyGuid(animalId, nameof(animalId));

        Guard.AgainstNullOrWhiteSpace(timepointLabel, nameof(timepointLabel));
        string trimmedLabel = timepointLabel.Trim();
        Guard.AgainstMaxLength(trimmedLabel, MaxTimepointLabelLength, nameof(timepointLabel));

        Guard.AgainstNullOrWhiteSpace(rawValue, nameof(rawValue));
        string trimmedRaw = rawValue.Trim();
        Guard.AgainstMaxLength(trimmedRaw, MaxRawValueLength, nameof(rawValue));

        Guard.AgainstNullOrWhiteSpace(recordedBy, nameof(recordedBy));
        string trimmedRecordedBy = recordedBy.Trim();
        Guard.AgainstMaxLength(trimmedRecordedBy, MaxRecordedByLength, nameof(recordedBy));

        return new BehavioralMeasurement(
            Guid.NewGuid(), animalId, trimmedLabel, trimmedRaw, trimmedRecordedBy, recordedAtUtc);
    }
}
