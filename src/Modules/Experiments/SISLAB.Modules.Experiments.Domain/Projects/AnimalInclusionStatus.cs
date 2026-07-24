namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// The inclusion state of an animal after a selection criterion is applied (SISLAB-02). Persisted by name so the
/// listing can filter included vs excluded and reordering never rewrites history.
/// </summary>
public enum AnimalInclusionStatus
{
    /// <summary>The animal qualified under the criterion (its reading satisfied the threshold).</summary>
    Included = 0,

    /// <summary>The animal did not qualify (its reading failed the threshold).</summary>
    Excluded = 1,
}
