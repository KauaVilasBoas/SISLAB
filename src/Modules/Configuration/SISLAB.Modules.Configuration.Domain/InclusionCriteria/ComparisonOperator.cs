namespace SISLAB.Modules.Configuration.Domain.InclusionCriteria;

/// <summary>
/// The comparison an <see cref="InclusionCriterion"/> applies between an animal's measured value and its threshold
/// (SISLAB-02). Persisted by name (stable string) so a reordering never rewrites history.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>The measured value must be greater than or equal to the threshold (e.g. glicemia ≥ 250).</summary>
    GreaterThanOrEqual = 0,

    /// <summary>The measured value must be strictly greater than the threshold.</summary>
    GreaterThan = 1,

    /// <summary>The measured value must be less than or equal to the threshold.</summary>
    LessThanOrEqual = 2,

    /// <summary>The measured value must be strictly less than the threshold.</summary>
    LessThan = 3,

    /// <summary>The measured value must equal the threshold.</summary>
    Equal = 4,
}
