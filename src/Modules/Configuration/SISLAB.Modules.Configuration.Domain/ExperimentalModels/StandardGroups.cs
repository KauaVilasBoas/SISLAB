using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// The default group design of an experimental model (SISLAB-04): the ordered collection of <see cref="StandardGroup"/>
/// arms (Naive + Controle + dose-response curve in the ND example). An immutable value object owning the collection
/// invariants — unique group names and at least one group — so the aggregate can never hold a duplicated or empty
/// design.
/// </summary>
/// <remarks>
/// <b>Order is meaningful.</b> The curve reads from Naive/Control through the descending doses, so insertion order
/// is preserved. Group names are unique case-insensitively within the model. The concrete arms are cadastered per
/// model; nothing here is a code constant.
/// </remarks>
public sealed class StandardGroups : ValueObject
{
    private const int MaxCount = 50;

    private readonly IReadOnlyList<StandardGroup> _groups;

    private StandardGroups(IReadOnlyList<StandardGroup> groups) => _groups = groups;

    /// <summary>The default groups, in the (meaningful) design order they were supplied in.</summary>
    public IReadOnlyList<StandardGroup> Groups => _groups;

    /// <summary>
    /// Builds the default group design, enforcing at least one group, no duplicate names (case-insensitive) and a
    /// sane upper bound.
    /// </summary>
    public static StandardGroups From(IEnumerable<StandardGroup>? groups)
    {
        List<StandardGroup> list = (groups ?? []).ToList();

        if (list.Count == 0)
            throw new DomainException("An experimental model must define at least one standard group.");

        if (list.Count > MaxCount)
            throw new DomainException($"An experimental model cannot define more than {MaxCount} standard groups.");

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (StandardGroup group in list)
        {
            if (!names.Add(group.Name))
                throw new DomainException($"Duplicate standard group name '{group.Name}' in the experimental model.");
        }

        return new StandardGroups(list);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
        => _groups.Cast<object?>();
}
