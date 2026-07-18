using SISLAB.Modules.Experiments.Domain.Biobank.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Domain.Biobank;

/// <summary>
/// Aggregate root for a biobank sample (card [E11] #89 — decision F4: the biobank is its <b>own</b> aggregate,
/// deliberately not reusing the Inventory's <c>StockItem</c>/<c>StorageLocation</c>). A sample is a biological
/// aliquot (blood, plasma, tissue, …) collected from a study <see cref="AnimalId"/> during an experiment's
/// collection step, kept under a conservation <see cref="ConservationRange"/> at an optional freezer
/// <see cref="StorageLabel"/>, and progressively consumed by the <see cref="Analysis"/>es run against it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived balance (decision F4).</b> The sample does not store a mutable "remaining" field: the
/// <see cref="RemainingQuantity"/> is <i>computed</i> as the collected amount minus the sum of its analyses'
/// consumed aliquots, so it can never drift out of sync with the analyses that spent it. Every consuming amount
/// shares the sample's unit (enforced by <see cref="SampleAmount"/>), so the balance is a plain subtraction.
/// </para>
/// <para>
/// <b>Ids by value.</b> The origin animal, project, batch and the collection experiment are held only by their
/// id (no cross-aggregate FK/navigation), exactly like the rest of the module — the biobank knows <i>which</i>
/// animal a sample came from without owning the in vivo design aggregate.
/// </para>
/// </remarks>
public sealed class Sample : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxCodeLength = 60;
    private const int MaxStorageLabelLength = 120;
    private const int MaxNotesLength = 2000;
    private const int MaxActorLength = 200;

    private readonly List<Analysis> _analyses = [];

    // Parameterless constructor for EF Core materialization.
    private Sample() : base(Guid.Empty)
    {
        Code = default!;
        CollectedQuantity = default!;
        CollectedBy = default!;
    }

    private Sample(
        Guid id,
        Guid companyId,
        string code,
        SampleType type,
        Guid projectId,
        Guid batchId,
        Guid animalId,
        Guid sourceExperimentId,
        SampleAmount collectedQuantity,
        TemperatureRange? conservationRange,
        string? storageLabel,
        string? notes,
        string collectedBy,
        DateTime collectedAtUtc)
        : base(id)
    {
        CompanyId = companyId;
        Code = code;
        Type = type;
        ProjectId = projectId;
        BatchId = batchId;
        AnimalId = animalId;
        SourceExperimentId = sourceExperimentId;
        CollectedQuantity = collectedQuantity;
        ConservationRange = conservationRange;
        StorageLabel = storageLabel;
        Notes = notes;
        CollectedBy = collectedBy;
        CollectedAtUtc = collectedAtUtc;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>Operator-facing sample code (freezer label), unique within the company.</summary>
    public string Code { get; private set; }

    /// <summary>The biological material the sample holds.</summary>
    public SampleType Type { get; private set; }

    /// <summary>The in vivo project the sample was collected in, referenced by value.</summary>
    public Guid ProjectId { get; private set; }

    /// <summary>The batch (leva) the sample was collected in, referenced by value.</summary>
    public Guid BatchId { get; private set; }

    /// <summary>The animal the sample was collected from, referenced by value.</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>The experiment whose collection step originated the sample, referenced by value.</summary>
    public Guid SourceExperimentId { get; private set; }

    /// <summary>The amount collected — the ceiling the derived balance is measured against.</summary>
    public SampleAmount CollectedQuantity { get; private set; }

    /// <summary>Conservation temperature range the sample must be kept at, or null when not specified.</summary>
    public TemperatureRange? ConservationRange { get; private set; }

    /// <summary>Free-text freezer/box location within the biobank, or null when not recorded.</summary>
    public string? StorageLabel { get; private set; }

    /// <summary>Optional free-text notes captured at collection.</summary>
    public string? Notes { get; private set; }

    /// <summary>Actor who collected the sample (identity claim).</summary>
    public string CollectedBy { get; private set; }

    /// <summary>Instant (UTC) the sample was collected.</summary>
    public DateTime CollectedAtUtc { get; private set; }

    /// <summary>The analyses run against the sample (each consumes an aliquot).</summary>
    public IReadOnlyList<Analysis> Analyses => _analyses.AsReadOnly();

    /// <summary>The total amount consumed by all analyses, in the sample's unit.</summary>
    public SampleAmount ConsumedQuantity =>
        _analyses.Aggregate(
            SampleAmount.Of(0m, CollectedQuantity.Unit),
            (running, analysis) => SampleAmount.Of(running.Value + analysis.ConsumedAmount.Value, running.Unit));

    /// <summary>
    /// The <b>derived</b> remaining balance: collected minus everything the analyses consumed. Never stored.
    /// </summary>
    public SampleAmount RemainingQuantity => CollectedQuantity.Subtract(ConsumedQuantity);

    /// <summary>True once the derived balance is exhausted.</summary>
    public bool IsDepleted => RemainingQuantity.IsZero;

    /// <summary>
    /// Creates a collected sample and raises the collection event. The collected amount must be strictly positive
    /// (an empty collection is not a sample); a supplied consuming amount later must share this unit.
    /// </summary>
    public static Sample Collect(
        Guid companyId,
        string code,
        SampleType type,
        Guid projectId,
        Guid batchId,
        Guid animalId,
        Guid sourceExperimentId,
        SampleAmount collectedQuantity,
        string collectedBy,
        DateTime collectedAtUtc,
        TemperatureRange? conservationRange = null,
        string? storageLabel = null,
        string? notes = null)
    {
        Guard.AgainstEmptyGuid(companyId, nameof(companyId));

        Guard.AgainstNullOrWhiteSpace(code, nameof(code));
        string trimmedCode = code.Trim();
        Guard.AgainstMaxLength(trimmedCode, MaxCodeLength, nameof(code));

        Guard.AgainstEmptyGuid(projectId, nameof(projectId));
        Guard.AgainstEmptyGuid(batchId, nameof(batchId));
        Guard.AgainstEmptyGuid(animalId, nameof(animalId));
        Guard.AgainstEmptyGuid(sourceExperimentId, nameof(sourceExperimentId));

        Guard.AgainstNull(collectedQuantity, nameof(collectedQuantity));
        if (collectedQuantity.IsZero)
            throw new DomainException("A collected sample must have a positive quantity.");

        Guard.AgainstNullOrWhiteSpace(collectedBy, nameof(collectedBy));
        string trimmedCollectedBy = collectedBy.Trim();
        Guard.AgainstMaxLength(trimmedCollectedBy, MaxActorLength, nameof(collectedBy));

        string? trimmedStorageLabel = Normalize(storageLabel, MaxStorageLabelLength, nameof(storageLabel));
        string? trimmedNotes = Normalize(notes, MaxNotesLength, nameof(notes));

        var sample = new Sample(
            Guid.NewGuid(),
            companyId,
            trimmedCode,
            type,
            projectId,
            batchId,
            animalId,
            sourceExperimentId,
            collectedQuantity,
            conservationRange,
            trimmedStorageLabel,
            trimmedNotes,
            trimmedCollectedBy,
            collectedAtUtc);

        sample.RaiseDomainEvent(new SampleCollectedEvent(
            sample.CompanyId, sample.Id, sample.Code, sample.AnimalId));

        return sample;
    }

    /// <summary>
    /// Runs an analysis against the sample, consuming <paramref name="consumedAmount"/> of its balance. The amount
    /// must share the sample's unit and must not exceed the remaining balance — the biobank never over-consumes a
    /// sample. Returns the created (pending) analysis.
    /// </summary>
    public Analysis Analyse(
        string name,
        SampleAmount consumedAmount,
        string performedBy,
        DateTime performedAtUtc)
    {
        Guard.AgainstNull(consumedAmount, nameof(consumedAmount));

        if (!consumedAmount.HasSameUnitAs(CollectedQuantity))
            throw new DomainException(
                $"Analysis amount unit '{consumedAmount.Unit}' does not match the sample unit " +
                $"'{CollectedQuantity.Unit}'.");

        if (consumedAmount.IsZero)
            throw new DomainException("An analysis must consume a positive amount of the sample.");

        if (!RemainingQuantity.CanCover(consumedAmount))
            throw new ConflictException(
                $"Sample '{Code}' has only {RemainingQuantity} left; cannot consume {consumedAmount}.");

        Analysis analysis = Analysis.Create(name, consumedAmount, performedBy, performedAtUtc);
        _analyses.Add(analysis);
        return analysis;
    }

    /// <summary>Records the result of one of the sample's analyses, signing it off as completed.</summary>
    public void RecordAnalysisResult(Guid analysisId, string result)
    {
        Analysis analysis = _analyses.FirstOrDefault(a => a.Id == analysisId)
            ?? throw new NotFoundException(
                $"Analysis '{analysisId}' was not found on sample '{Code}'.");

        analysis.RecordResult(result);
    }

    /// <summary>Sets the conservation temperature range (or clears it when null).</summary>
    public void DefineConservationRange(TemperatureRange? conservationRange)
        => ConservationRange = conservationRange;

    /// <summary>Sets or clears the freezer/box storage label.</summary>
    public void Relocate(string? storageLabel)
        => StorageLabel = Normalize(storageLabel, MaxStorageLabelLength, nameof(storageLabel));

    private static string? Normalize(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
