using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Collection;

/// <summary>
/// A single analysis a collection plan routes a sample type to (SISLAB-08) — e.g. "Hemograma", "ELISA", "PCR". It is a
/// child of a <see cref="SampleRouting"/>, itself a child of the <see cref="CollectionPlan"/> aggregate, so it is only
/// ever created through the aggregate. The name is the link to the biobank: the real <c>Analysis</c> a lab runs against
/// a collected sample carries the same name, which is how the status board (a read-side derivation) matches a planned
/// analysis to its actual, real state — the plan never keeps a parallel status of its own.
/// </summary>
/// <remarks>
/// Modelled as its own entity (rather than a raw string) so it maps cleanly to the <c>collection_planned_analyses</c>
/// child table with a surrogate key EF Core can track.
/// </remarks>
public sealed class PlannedAnalysis
{
    private const int MaxNameLength = 200;

    // Parameterless constructor for EF Core materialization.
    private PlannedAnalysis()
    {
    }

    private PlannedAnalysis(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Surrogate key for the child row (EF Core tracking); not domain-meaningful.</summary>
    public Guid Id { get; private init; }

    /// <summary>The planned assay name (matched by name to the biobank's real analyses for the status board).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Creates a planned analysis with a validated, trimmed name.</summary>
    public static PlannedAnalysis Named(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmed = name.Trim();
        Guard.AgainstMaxLength(trimmed, MaxNameLength, nameof(name));
        return new PlannedAnalysis(Guid.NewGuid(), trimmed);
    }

    /// <summary>Whether this planned analysis has <paramref name="name"/> (case-insensitively).</summary>
    public bool HasName(string name)
        => !string.IsNullOrWhiteSpace(name)
           && string.Equals(Name, name.Trim(), StringComparison.OrdinalIgnoreCase);
}
