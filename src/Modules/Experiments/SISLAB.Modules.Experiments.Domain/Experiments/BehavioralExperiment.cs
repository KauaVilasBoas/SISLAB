using SISLAB.Modules.Experiments.Domain.Experiments.Events;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// Abstract base for every in vivo behavioural assay (card [E11] #88): von Frey, tail-flick, rota-rod, hemogram.
/// It owns the state and behaviour these tests share — the <see cref="ProjectId"/> / <see cref="BatchId"/> the
/// experiment runs on (held <b>by value</b>), the collection of raw per-timepoint <see cref="BehavioralMeasurement"/>s
/// authored by whoever launched each timepoint, and the frozen <see cref="CalculationResult"/> snapshot — so a new
/// behavioural test is a thin subtype that only seeds its step titles, never a re-implementation of the flow.
/// </summary>
/// <remarks>
/// <para>
/// Behaviour that varies by type is <b>not</b> here: the calculation (e.g. the von Frey up-down 50% threshold) is a
/// versioned Strategy (<c>IExperimentProtocol</c>) resolved by <see cref="Experiment.Type"/> in the application
/// layer, which returns the <see cref="FormulaSnapshot"/> this aggregate stores immutably via
/// <see cref="ApplyCalculation"/> (herança para estado, Strategy para cálculo — decision card #68, applied to in
/// vivo). Tests that need no calculation (a plain latency) simply have their raw values exported as-is.
/// </para>
/// <para>
/// Recording a timepoint captures one measurement per animal and marks the matching timepoint step as performed,
/// moving the experiment to <see cref="ExperimentStatus.AwaitingCalculation"/> — the in vivo hand-off. The snapshot
/// is frozen once and never recomputed (reproducibility): applying a calculation twice is rejected.
/// </para>
/// </remarks>
public abstract class BehavioralExperiment : Experiment
{
    private readonly List<BehavioralMeasurement> _measurements = [];

    // Parameterless constructor for EF Core materialization.
    protected BehavioralExperiment() { }

    protected BehavioralExperiment(
        Guid id,
        ExperimentType type,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid projectId,
        Guid batchId)
        : base(id, type, title, description, createdBy, createdAtUtc)
    {
        ProjectId = projectId;
        BatchId = batchId;
    }

    /// <summary>The in vivo project this experiment runs on, referenced by value (no cross-aggregate FK).</summary>
    public Guid ProjectId { get; private set; }

    /// <summary>The batch (leva) this experiment runs on, referenced by value (no cross-aggregate FK).</summary>
    public Guid BatchId { get; private set; }

    /// <summary>Frozen result of the versioned calculation, or null until it has run.</summary>
    public FormulaSnapshot? CalculationResult { get; private set; }

    /// <summary>The raw per-timepoint readings captured across the experiment's animals.</summary>
    public IReadOnlyList<BehavioralMeasurement> Measurements => _measurements.AsReadOnly();

    /// <summary>True once at least one timepoint has been recorded.</summary>
    public bool HasMeasurements => _measurements.Count > 0;

    /// <summary>
    /// Seeds the shared behavioural flow: a baseline timepoint, the requested follow-up timepoints, then the
    /// calculation and analysis steps. Called by each subtype's factory so the skeleton is defined once.
    /// </summary>
    protected void SeedBehavioralSteps(
        IReadOnlyList<string> timepointLabels,
        string calculationTitle,
        string analysisTitle)
    {
        Guard.AgainstNull(timepointLabels, nameof(timepointLabels));

        if (timepointLabels.Count == 0)
            throw new DomainException("A behavioural experiment must declare at least one timepoint.");

        int order = 0;
        foreach (string label in timepointLabels)
            AddStep(ExperimentStep.Create(order++, ExperimentStepKind.Timepoint, label));

        AddStep(ExperimentStep.Create(order++, ExperimentStepKind.Calculation, calculationTitle));
        AddStep(ExperimentStep.Create(order, ExperimentStepKind.Analysis, analysisTitle));
    }

    /// <summary>
    /// Records the readings of a single timepoint: one <see cref="BehavioralMeasurement"/> per supplied
    /// (animal, raw value) pair, authored by <paramref name="actor"/>. Marks the matching timepoint step as
    /// performed, moves a draft into progress and advances the experiment to
    /// <see cref="ExperimentStatus.AwaitingCalculation"/> (the hand-off to the calculator). Re-recording a
    /// timepoint replaces its previous readings so a mistaken launch can be corrected before calculation.
    /// </summary>
    public void RecordTimepoint(
        string timepointLabel,
        IEnumerable<(Guid AnimalId, string RawValue)> readings,
        string actor,
        DateTime performedAtUtc)
    {
        Guard.AgainstNullOrWhiteSpace(timepointLabel, nameof(timepointLabel));
        ArgumentNullException.ThrowIfNull(readings);
        Guard.AgainstNullOrWhiteSpace(actor, nameof(actor));

        if (CalculationResult is not null)
            throw new ConflictException(
                $"Experiment '{Title}' has already been calculated; its measurements are frozen.");

        ExperimentStep step = FindStep(ExperimentStepKind.Timepoint, timepointLabel)
            ?? throw new NotFoundException(
                $"Timepoint '{timepointLabel}' is not part of experiment '{Title}'.");

        var recorded = readings
            .Select(reading => BehavioralMeasurement.Create(
                reading.AnimalId, timepointLabel, reading.RawValue, actor, performedAtUtc))
            .ToList();

        if (recorded.Count == 0)
            throw new DomainException($"Timepoint '{timepointLabel}' must record at least one reading.");

        // Replace any prior readings for this timepoint (idempotent re-launch).
        _measurements.RemoveAll(measurement =>
            string.Equals(measurement.TimepointLabel, step.Title, StringComparison.OrdinalIgnoreCase));
        _measurements.AddRange(recorded);

        step.MarkPerformed(actor, performedAtUtc);

        if (Status == ExperimentStatus.Draft)
            Start();

        if (Status == ExperimentStatus.InProgress)
            TransitionTo(ExperimentStatus.AwaitingCalculation);
    }

    /// <summary>
    /// Records that a biobank collection was carried out on this experiment (card [E11] #89): appends (once) a
    /// <see cref="ExperimentStepKind.Collection"/> step and marks it performed by <paramref name="actor"/>. The
    /// <see cref="Biobank.Sample"/>s themselves are their own aggregate — this only records the collection
    /// hand-off on the experiment's flow, keeping the two aggregates decoupled (the sample holds the experiment id
    /// by value). Re-recording a collection simply refreshes the step authorship.
    /// </summary>
    public void RecordCollection(string label, string actor, DateTime performedAtUtc)
    {
        Guard.AgainstNullOrWhiteSpace(label, nameof(label));
        Guard.AgainstNullOrWhiteSpace(actor, nameof(actor));

        ExperimentStep step = FindStep(ExperimentStepKind.Collection, label)
            ?? AddCollectionStep(label);

        step.MarkPerformed(actor, performedAtUtc);

        if (Status == ExperimentStatus.Draft)
            Start();
    }

    private ExperimentStep AddCollectionStep(string label)
    {
        int nextOrder = Steps.Count == 0 ? 0 : Steps.Max(step => step.Order) + 1;
        ExperimentStep step = ExperimentStep.Create(nextOrder, ExperimentStepKind.Collection, label);
        AddStep(step);
        return step;
    }

    /// <summary>
    /// Stores the frozen calculation snapshot produced by the versioned protocol, marks the calculation step as
    /// performed and moves the experiment to <see cref="ExperimentStatus.AwaitingAnalysis"/>. Requires recorded
    /// measurements and rejects recalculation of an already-calculated experiment — the snapshot is immutable.
    /// </summary>
    public void ApplyCalculation(FormulaSnapshot snapshot, string actor)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Guard.AgainstNullOrWhiteSpace(actor, nameof(actor));

        if (CalculationResult is not null)
            throw new ConflictException(
                $"Experiment '{Title}' has already been calculated and its result is immutable.");

        if (!HasMeasurements)
            throw new DomainException("The calculation requires at least one recorded timepoint.");

        CalculationResult = snapshot;

        FindStep(ExperimentStepKind.Calculation)?.MarkPerformed(actor, snapshot.AppliedAtUtc);

        if (Status == ExperimentStatus.AwaitingCalculation)
            TransitionTo(ExperimentStatus.AwaitingAnalysis);

        RaiseDomainEvent(new ExperimentCalculatedEvent(
            CompanyId, Id, Type, snapshot.FormulaName, snapshot.AppliedAtUtc));
    }
}
