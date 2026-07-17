namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// In vitro cell-viability experiment (decision card #68 — the thin vertical slice). A TPH subtype of
/// <see cref="PlateExperiment"/>: it inherits the whole plate lifecycle (8×12 plate, reader import, frozen
/// calculation snapshot, four-step flow) and only pins the <see cref="ExperimentType.ViabilidadeCelular"/>
/// discriminator and its human-readable step titles.
/// </summary>
/// <remarks>
/// The viability formula itself is <b>not</b> here — it is the versioned <c>viability@v1</c> Strategy
/// (<c>IExperimentProtocol</c>) resolved by type in the application layer, which returns the
/// <see cref="FormulaSnapshot"/> the base then stores immutably. Keeping the plate machinery on the base means
/// nitric oxide and any future plate assay reuse it verbatim (decision card #68).
/// </remarks>
public sealed class ViabilidadeCelularExperiment : PlateExperiment
{
    // Parameterless constructor for EF Core materialization.
    private ViabilidadeCelularExperiment() { }

    private ViabilidadeCelularExperiment(
        Guid id,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid? compoundPartnerId)
        : base(id, ExperimentType.ViabilidadeCelular, title, description, createdBy, createdAtUtc, compoundPartnerId)
    {
    }

    /// <summary>
    /// Creates a viability experiment in <see cref="ExperimentStatus.Draft"/> with an empty plate, seeds its
    /// default step flow (design → reader import → calculation → analysis) and raises the creation event.
    /// </summary>
    public static ViabilidadeCelularExperiment Create(
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid? compoundPartnerId = null)
    {
        (string normalizedTitle, string? normalizedDescription, string normalizedCreatedBy) =
            NormalizeCreation(title, description, createdBy);

        var experiment = new ViabilidadeCelularExperiment(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedDescription,
            normalizedCreatedBy,
            createdAtUtc,
            compoundPartnerId);

        experiment.SeedPlateSteps("Plate design", "Reader import", "Viability calculation", "Analysis / export");

        experiment.RaiseCreated();
        return experiment;
    }
}
