namespace SISLAB.Modules.Configuration.Contracts;

/// <summary>
/// Public, flattened view of a tenant's animal-inclusion criterion (SISLAB-02), returned across the module boundary
/// by <see cref="ILabConfiguration"/>. It carries only primitives — never the internal <c>InclusionCriterion</c>
/// aggregate or its value objects — so the consuming module (Experiments) depends on nothing of the Configuration
/// Domain (module isolation, section 2). The <see cref="Operator"/> is the stable string code of the comparison, and
/// <see cref="Includes"/> encapsulates the comparison so callers never re-implement it.
/// </summary>
/// <param name="ParameterCode">The physiological parameter the criterion selects on (e.g. "glicemia").</param>
/// <param name="Operator">The comparison as a stable string code (e.g. "GreaterThanOrEqual").</param>
/// <param name="Threshold">The numeric threshold a reading is compared against.</param>
/// <param name="Unit">The unit the threshold is expressed in (e.g. "mg/dL").</param>
public sealed record InclusionCriterionDto(string ParameterCode, string Operator, decimal Threshold, string Unit)
{
    /// <summary>Whether this criterion is for <paramref name="parameterCode"/> (case-insensitively).</summary>
    public bool IsForParameter(string parameterCode)
        => !string.IsNullOrWhiteSpace(parameterCode)
           && string.Equals(ParameterCode, parameterCode.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="measuredValue"/> qualifies the animal under this criterion. The comparison lives here
    /// (mirroring the domain's <c>InclusionThreshold</c>) so the Experiments selection never parses the operator
    /// string itself — it asks the criterion. An unrecognized operator conservatively excludes (returns false).
    /// </summary>
    public bool Includes(decimal measuredValue) => Operator switch
    {
        "GreaterThanOrEqual" => measuredValue >= Threshold,
        "GreaterThan" => measuredValue > Threshold,
        "LessThanOrEqual" => measuredValue <= Threshold,
        "LessThan" => measuredValue < Threshold,
        "Equal" => measuredValue == Threshold,
        _ => false,
    };
}
