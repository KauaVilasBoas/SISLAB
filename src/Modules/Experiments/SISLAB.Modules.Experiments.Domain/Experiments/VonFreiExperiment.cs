namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// In vivo von Frey mechanical-allodynia experiment (card [E11] #88). A TPH subtype of
/// <see cref="BehavioralExperiment"/>: it inherits the whole per-timepoint measurement lifecycle and only pins the
/// <see cref="ExperimentType.VonFrei"/> discriminator and its step titles.
/// </summary>
/// <remarks>
/// The 50% withdrawal threshold is computed by the up-down method (Dixon/Chaplan): each animal's raw value is its
/// stimulus/response series, and the versioned <c>von-frey-up-down@v1</c> Strategy turns it into a threshold in
/// grams. That calculation lives in the Strategy, not on the aggregate — so von Frey shares this identical
/// behavioural lifecycle with the latency-based tests (decision card #68 applied to in vivo).
/// </remarks>
public sealed class VonFreiExperiment : BehavioralExperiment
{
    // Parameterless constructor for EF Core materialization.
    private VonFreiExperiment() { }

    private VonFreiExperiment(
        Guid id,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc,
        Guid projectId,
        Guid batchId)
        : base(id, ExperimentType.VonFrei, title, description, createdBy, createdAtUtc, projectId, batchId)
    {
    }

    /// <summary>
    /// Creates a von Frey experiment in <see cref="ExperimentStatus.Draft"/>, seeds its timepoint flow (a baseline
    /// plus the requested follow-ups) and the calculation/analysis steps, and raises the creation event.
    /// </summary>
    public static VonFreiExperiment Create(
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

        var experiment = new VonFreiExperiment(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedDescription,
            normalizedCreatedBy,
            createdAtUtc,
            projectId,
            batchId);

        experiment.SeedBehavioralSteps(
            timepointLabels,
            "von Frey up-down calculation (50% threshold)",
            "Analysis / export");

        experiment.RaiseCreated();
        return experiment;
    }
}
