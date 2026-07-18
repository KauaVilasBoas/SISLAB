using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Biobank;

/// <summary>
/// A single analysis run against a biobank <see cref="Sample"/> (card [E11] #89): a named assay (e.g. "ELISA
/// TNF-α", "Western blot") that consumes a <see cref="ConsumedAmount"/> aliquot of the sample. It records the
/// authorship of the run (<see cref="PerformedBy"/> / <see cref="PerformedAtUtc"/>) and, once read out, its
/// free-text <see cref="Result"/>.
/// </summary>
/// <remarks>
/// An analysis is a child entity of the <see cref="Sample"/> aggregate, only ever created and completed through
/// it. The consumed aliquot is what drives the sample's <b>derived</b> remaining balance — the aggregate sums the
/// consumed amounts of its analyses and subtracts them from what was collected, so the balance is never a stored
/// field that can drift out of sync.
/// </remarks>
public sealed class Analysis : Entity<Guid>
{
    private const int MaxNameLength = 200;
    private const int MaxResultLength = 4000;
    private const int MaxActorLength = 200;

    // Parameterless constructor for EF Core materialization.
    private Analysis() : base(Guid.Empty)
    {
        Name = default!;
        ConsumedAmount = default!;
        PerformedBy = default!;
    }

    private Analysis(
        Guid id,
        string name,
        SampleAmount consumedAmount,
        string performedBy,
        DateTime performedAtUtc)
        : base(id)
    {
        Name = name;
        ConsumedAmount = consumedAmount;
        PerformedBy = performedBy;
        PerformedAtUtc = performedAtUtc;
        Status = AnalysisStatus.Pending;
    }

    /// <summary>Human-readable name of the assay run against the sample.</summary>
    public string Name { get; private set; }

    /// <summary>The aliquot of the sample this analysis consumes (drives the derived balance).</summary>
    public SampleAmount ConsumedAmount { get; private set; }

    /// <summary>Actor who ran the analysis (identity claim).</summary>
    public string PerformedBy { get; private set; }

    /// <summary>Instant (UTC) the analysis was run.</summary>
    public DateTime PerformedAtUtc { get; private set; }

    /// <summary>Free-text result once read out, or null while the analysis is pending.</summary>
    public string? Result { get; private set; }

    /// <summary>Lifecycle of the analysis (pending until its result is recorded).</summary>
    public AnalysisStatus Status { get; private set; }

    /// <summary>Creates a pending analysis consuming the given aliquot, authored by the actor.</summary>
    internal static Analysis Create(
        string name,
        SampleAmount consumedAmount,
        string performedBy,
        DateTime performedAtUtc)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Guard.AgainstNull(consumedAmount, nameof(consumedAmount));

        Guard.AgainstNullOrWhiteSpace(performedBy, nameof(performedBy));
        string trimmedActor = performedBy.Trim();
        Guard.AgainstMaxLength(trimmedActor, MaxActorLength, nameof(performedBy));

        return new Analysis(Guid.NewGuid(), trimmedName, consumedAmount, trimmedActor, performedAtUtc);
    }

    /// <summary>
    /// Records the analysis' result and signs it off as <see cref="AnalysisStatus.Completed"/>. Idempotent
    /// re-recording refreshes the result; the consumed aliquot is fixed at creation and never changes.
    /// </summary>
    internal void RecordResult(string result)
    {
        Guard.AgainstNullOrWhiteSpace(result, nameof(result));
        string trimmedResult = result.Trim();
        Guard.AgainstMaxLength(trimmedResult, MaxResultLength, nameof(result));

        Result = trimmedResult;
        Status = AnalysisStatus.Completed;
    }
}
