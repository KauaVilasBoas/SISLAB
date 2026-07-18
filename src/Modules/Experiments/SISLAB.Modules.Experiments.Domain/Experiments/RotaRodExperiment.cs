namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// In vivo rota-rod motor-coordination experiment (card [E11] #88). A TPH subtype of
/// <see cref="BehavioralExperiment"/> that pins the <see cref="ExperimentType.RotaRod"/> discriminator and its
/// step titles. Each animal's raw value is a latency-to-fall in seconds; the export uses the values as recorded.
/// </summary>
public sealed class RotaRodExperiment : BehavioralExperiment
{
    // Parameterless constructor for EF Core materialization.
    private RotaRodExperiment() { }

    private RotaRodExperiment(
        Guid id,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid projectId,
        Guid batchId)
        : base(id, ExperimentType.RotaRod, title, description, createdBy, createdAtUtc, projectId, batchId)
    {
    }

    /// <summary>Creates a rota-rod experiment, seeds its timepoint/calculation/analysis flow and raises created.</summary>
    public static RotaRodExperiment Create(
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

        var experiment = new RotaRodExperiment(
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
