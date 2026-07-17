using SISLAB.Modules.Experiments.Domain.Experiments.Events;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// Abstract base for every plate-based assay (decision card #68). It owns the state and behaviour that the
/// in vitro microplate assays share — the 8×12 <see cref="Plate"/>, the optional partner compound under test
/// (<see cref="CompoundPartnerId"/>, held by value), the frozen <see cref="CalculationResult"/> snapshot and the
/// design → import → calculate → analysis flow — so a new plate assay (viability, nitric oxide, …) is a thin
/// subtype that only seeds its step titles, never a re-implementation of the plate machinery.
/// </summary>
/// <remarks>
/// <para>
/// Behaviour that varies by type is <b>not</b> here: the formula is a versioned Strategy (<c>IExperimentProtocol</c>)
/// resolved by <see cref="Experiment.Type"/> in the application layer, which returns the <see cref="FormulaSnapshot"/>
/// this aggregate stores immutably via <see cref="ApplyCalculation"/>. What differs between viability and nitric
/// oxide is the <i>meaning</i> of the well roles and the calculation — both live outside the aggregate — so the two
/// subtypes share this identical plate lifecycle (decision card #68: herança para estado, Strategy para cálculo).
/// </para>
/// <para>
/// The snapshot is frozen once and never recomputed (reproducibility): applying a calculation to an
/// already-calculated experiment is rejected.
/// </para>
/// </remarks>
public abstract class PlateExperiment : Experiment
{
    // Parameterless constructor for EF Core materialization.
    protected PlateExperiment() => Plate = new Plate();

    protected PlateExperiment(
        Guid id,
        ExperimentType type,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid? compoundPartnerId)
        : base(id, type, title, description, createdBy, createdAtUtc)
    {
        Plate = new Plate();
        CompoundPartnerId = NormalizeCompound(compoundPartnerId);
    }

    /// <summary>The 8×12 microplate owned by this experiment.</summary>
    public Plate Plate { get; private set; }

    /// <summary>
    /// The partner compound under test, referenced <b>by value</b> (the Inventory partner-item id). Optional,
    /// and never a cross-module FK/navigation — a plain uuid, exactly like Inventory holds <c>experimentId</c>.
    /// </summary>
    public Guid? CompoundPartnerId { get; private set; }

    /// <summary>Frozen result of the versioned calculation, or null until it has run.</summary>
    public FormulaSnapshot? CalculationResult { get; private set; }

    /// <summary>True once the plate has been designed and every well has an imported reading.</summary>
    public bool IsReadyToCalculate => Plate.HasCompleteReading;

    /// <summary>
    /// Seeds the shared plate flow (design → reader import → calculation → analysis) with the type's step
    /// titles. Called by each subtype's factory so the four-step skeleton is defined once.
    /// </summary>
    protected void SeedPlateSteps(string designTitle, string importTitle, string calculationTitle, string analysisTitle)
    {
        AddStep(ExperimentStep.Create(0, ExperimentStepKind.Baseline, designTitle));
        AddStep(ExperimentStep.Create(1, ExperimentStepKind.Measurement, importTitle));
        AddStep(ExperimentStep.Create(2, ExperimentStepKind.Calculation, calculationTitle));
        AddStep(ExperimentStep.Create(3, ExperimentStepKind.Analysis, analysisTitle));
    }

    /// <summary>
    /// Replaces the plate layout with the supplied wells and moves the experiment into execution if it was
    /// still a draft. Recording the plate design marks the baseline step as performed by <paramref name="actor"/>.
    /// </summary>
    public void DesignPlate(IEnumerable<Well> wells, string actor, DateTime performedAtUtc)
    {
        Plate.Design(wells);

        FindStep(ExperimentStepKind.Baseline)?.MarkPerformed(actor, performedAtUtc);

        if (Status == ExperimentStatus.Draft)
            Start();
    }

    /// <summary>Applies the plate reader's absorbance for a single well coordinate. Requires a designed plate.</summary>
    public void RecordWellAbsorbance(string coordinate, decimal rawAbsorbance)
    {
        if (!Plate.IsDesigned)
            throw new DomainException("The plate must be designed before importing a reading.");

        Plate.RecordAbsorbance(coordinate, rawAbsorbance);
    }

    /// <summary>
    /// Marks the reader-import step as performed once a reading batch has been applied. Kept separate from the
    /// per-well record so the command can apply many wells and then close the step once.
    /// </summary>
    public void MarkReadingImported(string actor, DateTime performedAtUtc)
        => FindStep(ExperimentStepKind.Measurement)?.MarkPerformed(actor, performedAtUtc);

    /// <summary>
    /// Stores the frozen calculation snapshot produced by the versioned protocol, marks the calculation step as
    /// performed and moves the experiment to <see cref="ExperimentStatus.AwaitingAnalysis"/> (the hand-off to the
    /// human analysis). Requires a complete plate reading and rejects recalculation of an already-calculated
    /// experiment — the snapshot is immutable (reproducibility, decision card #68).
    /// </summary>
    public void ApplyCalculation(FormulaSnapshot snapshot, string actor)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (CalculationResult is not null)
            throw new ConflictException(
                $"Experiment '{Title}' has already been calculated and its result is immutable.");

        if (!IsReadyToCalculate)
            throw new DomainException(
                "The calculation requires every designed well to have an imported absorbance.");

        CalculationResult = snapshot;

        FindStep(ExperimentStepKind.Calculation)?.MarkPerformed(actor, snapshot.AppliedAtUtc);

        if (Status == ExperimentStatus.InProgress)
            TransitionTo(ExperimentStatus.AwaitingAnalysis);

        RaiseDomainEvent(new ExperimentCalculatedEvent(
            CompanyId, Id, Type, snapshot.FormulaName, snapshot.AppliedAtUtc));
    }

    private static Guid? NormalizeCompound(Guid? compoundPartnerId)
        => compoundPartnerId is { } id && id != Guid.Empty ? id : null;
}
