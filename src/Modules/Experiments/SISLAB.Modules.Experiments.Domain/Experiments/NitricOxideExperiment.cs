namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// In vitro nitric-oxide (Griess) experiment (card [E11] #72). A TPH subtype of <see cref="PlateExperiment"/> —
/// same 8×12 plate, reader import and frozen-snapshot lifecycle as viability — that pins the
/// <see cref="ExperimentType.NitricOxide"/> discriminator and its step titles.
/// </summary>
/// <remarks>
/// The Griess method reads each well's absorbance and converts it to a nitrite (NO) concentration against a
/// calibration curve built from the plate's <see cref="Plates.WellRole.Standard"/> wells (sodium-nitrite points of
/// known µM). That curve and the linear regression live in the versioned <c>nitric-oxide@v1</c> Strategy, not on
/// the aggregate — so no NO-specific state is needed here beyond what the base already owns (the plate carries the
/// standards' <c>ConcentrationUm</c>, and the frozen result lands in the inherited <c>CalculationResult</c>).
/// </remarks>
public sealed class NitricOxideExperiment : PlateExperiment
{
    // Parameterless constructor for EF Core materialization.
    private NitricOxideExperiment() { }

    private NitricOxideExperiment(
        Guid id,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid? compoundPartnerId)
        : base(id, ExperimentType.NitricOxide, title, description, createdBy, createdAtUtc, compoundPartnerId)
    {
    }

    /// <summary>
    /// Creates a nitric-oxide experiment in <see cref="ExperimentStatus.Draft"/> with an empty plate, seeds its
    /// default step flow (design → reader import → NO calculation → analysis) and raises the creation event.
    /// </summary>
    public static NitricOxideExperiment Create(
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid? compoundPartnerId = null)
    {
        (string normalizedTitle, string? normalizedDescription, string normalizedCreatedBy) =
            NormalizeCreation(title, description, createdBy);

        var experiment = new NitricOxideExperiment(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedDescription,
            normalizedCreatedBy,
            createdAtUtc,
            compoundPartnerId);

        experiment.SeedPlateSteps("Plate design", "Reader import", "Nitric oxide calculation", "Analysis / export");

        experiment.RaiseCreated();
        return experiment;
    }
}
