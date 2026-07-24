using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Collection;

/// <summary>
/// One row of the collection matrix (SISLAB-08): a routing that says, for a given <see cref="SampleType"/>, which
/// analyses are planned and where the sample is stored. It is the digital form of the spreadsheet's collection sheet —
/// e.g. "Sangue → Hemograma/bioquímica → Fiocruz / −20 °C", "Medula → ELISA → −80 °C". A child entity of the
/// <see cref="CollectionPlan"/> aggregate, mutated only through it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Storage by value.</b> The storage location is a Configuration <c>Room</c> (a lab/freezer room) referenced only by
/// its <see cref="StorageRoomId"/> — never a cross-module FK or navigation (module isolation, section 2). Its existence
/// is validated in the application layer through the Configuration <c>ILabConfiguration</c> port. A free-text
/// <see cref="StorageLabel"/> (e.g. "−20 °C", "processado") complements it for the box/condition the room does not
/// capture, and an optional <see cref="ConservationRange"/> pins the temperature.
/// </para>
/// <para>
/// <b>Planned, not concrete.</b> The planned analyses are names, not real biobank <c>Analysis</c> records: they are the
/// intent. The status board matches them by name to the sample's real analyses, so the plan never duplicates the
/// biobank's actual state.
/// </para>
/// </remarks>
public sealed class SampleRouting
{
    private const int MaxStorageLabelLength = 120;

    private readonly List<PlannedAnalysis> _plannedAnalyses = [];

    // Parameterless constructor for EF Core materialization.
    private SampleRouting()
    {
    }

    private SampleRouting(
        Guid id,
        SampleType sampleType,
        Guid? storageRoomId,
        string? storageLabel,
        TemperatureRange? conservationRange)
    {
        Id = id;
        SampleType = sampleType;
        StorageRoomId = storageRoomId;
        StorageLabel = storageLabel;
        ConservationRange = conservationRange;
    }

    /// <summary>Surrogate key for the routing row (EF Core tracking); not domain-meaningful.</summary>
    public Guid Id { get; private init; }

    /// <summary>The biological material this routing applies to (unique within a plan).</summary>
    public SampleType SampleType { get; private set; }

    /// <summary>Optional Configuration room (lab/freezer) the sample is stored in, referenced by value.</summary>
    public Guid? StorageRoomId { get; private set; }

    /// <summary>Optional free-text box/condition label (e.g. "−20 °C", "processado") complementing the room.</summary>
    public string? StorageLabel { get; private set; }

    /// <summary>Optional conservation temperature range the sample must be kept at.</summary>
    public TemperatureRange? ConservationRange { get; private set; }

    /// <summary>The analyses planned for this sample type (at least one).</summary>
    public IReadOnlyList<PlannedAnalysis> PlannedAnalyses => _plannedAnalyses.AsReadOnly();

    /// <summary>
    /// Creates a routing for <paramref name="sampleType"/> planning the given analyses. At least one analysis is
    /// required — a routing that plans nothing is not a routing — and duplicate names collapse to one.
    /// </summary>
    internal static SampleRouting For(
        SampleType sampleType,
        IEnumerable<string> plannedAnalyses,
        Guid? storageRoomId,
        string? storageLabel,
        TemperatureRange? conservationRange)
    {
        Guard.AgainstNull(plannedAnalyses, nameof(plannedAnalyses));

        var routing = new SampleRouting(
            Guid.NewGuid(),
            sampleType,
            NormalizeRoomId(storageRoomId),
            NormalizeLabel(storageLabel),
            conservationRange);

        routing.ReplacePlannedAnalyses(plannedAnalyses);

        return routing;
    }

    /// <summary>Whether this routing plans an analysis called <paramref name="name"/> (case-insensitively).</summary>
    public bool Plans(string name) => _plannedAnalyses.Any(analysis => analysis.HasName(name));

    /// <summary>Replaces the storage location (room + label + conservation range) of this routing.</summary>
    internal void ChangeStorage(Guid? storageRoomId, string? storageLabel, TemperatureRange? conservationRange)
    {
        StorageRoomId = NormalizeRoomId(storageRoomId);
        StorageLabel = NormalizeLabel(storageLabel);
        ConservationRange = conservationRange;
    }

    /// <summary>Replaces the planned analyses, keeping the "at least one, distinct by name" invariant.</summary>
    internal void ReplacePlannedAnalyses(IEnumerable<string> plannedAnalyses)
    {
        Guard.AgainstNull(plannedAnalyses, nameof(plannedAnalyses));

        var distinct = new List<PlannedAnalysis>();
        foreach (string name in plannedAnalyses)
        {
            PlannedAnalysis analysis = PlannedAnalysis.Named(name);
            if (!distinct.Any(existing => existing.HasName(analysis.Name)))
                distinct.Add(analysis);
        }

        if (distinct.Count == 0)
            throw new DomainException("A sample routing must plan at least one analysis.");

        _plannedAnalyses.Clear();
        _plannedAnalyses.AddRange(distinct);
    }

    private static Guid? NormalizeRoomId(Guid? storageRoomId)
        => storageRoomId is { } id && id != Guid.Empty ? id : null;

    private static string? NormalizeLabel(string? storageLabel)
    {
        if (string.IsNullOrWhiteSpace(storageLabel))
            return null;

        string trimmed = storageLabel.Trim();
        Guard.AgainstMaxLength(trimmed, MaxStorageLabelLength, nameof(storageLabel));
        return trimmed;
    }
}
