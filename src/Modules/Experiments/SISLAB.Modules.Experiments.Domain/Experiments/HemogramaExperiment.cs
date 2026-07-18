namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// In vivo hemogram (complete blood count) experiment (card [E11] #88). A TPH subtype of
/// <see cref="BehavioralExperiment"/> that pins the <see cref="ExperimentType.Hemograma"/> discriminator and its
/// step titles. Each animal's raw value is its CBC readout at the timepoint (typically a single collection point);
/// the export uses the values as recorded.
/// </summary>
public sealed class HemogramaExperiment : BehavioralExperiment
{
    // Parameterless constructor for EF Core materialization.
    private HemogramaExperiment() { }

    private HemogramaExperiment(
        Guid id,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid projectId,
        Guid batchId)
        : base(id, ExperimentType.Hemograma, title, description, createdBy, createdAtUtc, projectId, batchId)
    {
    }

    /// <summary>Creates a hemogram experiment, seeds its timepoint/calculation/analysis flow and raises created.</summary>
    public static HemogramaExperiment Create(
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid projectId,
        Guid batchId,
        IReadOnlyList<string> timepointLabels)
    {
        (string normalizedTitle, string? normalizedDescription, string normalizedCreatedBy) =
            NormalizeCreation(title, description, createdBy);

        var experiment = new HemogramaExperiment(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedDescription,
            normalizedCreatedBy,
            createdAtUtc,
            projectId,
            batchId);

        experiment.SeedBehavioralSteps(timepointLabels, "Hemogram dataset review", "Analysis / export");

        experiment.RaiseCreated();
        return experiment;
    }
}
