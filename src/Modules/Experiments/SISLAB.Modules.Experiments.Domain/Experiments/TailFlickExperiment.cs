namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// In vivo tail-flick thermal-nociception experiment (card [E11] #88). A TPH subtype of
/// <see cref="BehavioralExperiment"/> that pins the <see cref="ExperimentType.TailFlick"/> discriminator and its
/// step titles. Each animal's raw value is a withdrawal latency in seconds; the export uses the values as recorded
/// (no threshold calculation is required), so its calculation step simply confirms the dataset is ready.
/// </summary>
public sealed class TailFlickExperiment : BehavioralExperiment
{
    // Parameterless constructor for EF Core materialization.
    private TailFlickExperiment() { }

    private TailFlickExperiment(
        Guid id,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid projectId,
        Guid batchId)
        : base(id, ExperimentType.TailFlick, title, description, createdBy, createdAtUtc, projectId, batchId)
    {
    }

    /// <summary>Creates a tail-flick experiment, seeds its timepoint/calculation/analysis flow and raises created.</summary>
    public static TailFlickExperiment Create(
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

        var experiment = new TailFlickExperiment(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedDescription,
            normalizedCreatedBy,
            createdAtUtc,
            projectId,
            batchId);

        experiment.SeedBehavioralSteps(timepointLabels, "Latency dataset review", "Analysis / export");

        experiment.RaiseCreated();
        return experiment;
    }
}
