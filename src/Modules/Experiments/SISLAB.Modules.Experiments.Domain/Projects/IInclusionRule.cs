namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A domain-local view of an inclusion criterion the <see cref="Project"/> aggregate applies to select animals
/// (SISLAB-02): the parameter it decides on and the predicate that says whether a measured value qualifies. The
/// aggregate depends on this abstraction, never on the Configuration module — the concrete rule (built from a
/// Configuration <c>InclusionCriterionDto</c>) is adapted in the Experiments application layer, so the module
/// isolation rule holds (Experiments Domain references no other module).
/// </summary>
/// <remarks>
/// Keeping the predicate behind an interface (a small Strategy) lets the aggregate stay agnostic of the operator kind
/// (≥, &gt;, …): it asks the rule "does this value qualify?" rather than switching on an operator it should not know
/// about. The concrete comparison lives in Configuration and is exercised through <see cref="QualifiedBy"/>.
/// </remarks>
public interface IInclusionRule
{
    /// <summary>The physiological parameter this rule decides on (e.g. "glicemia").</summary>
    string ParameterCode { get; }

    /// <summary>Whether this rule decides on <paramref name="parameterCode"/> (case-insensitively).</summary>
    bool AppliesTo(string parameterCode);

    /// <summary>Whether <paramref name="measuredValue"/> qualifies the animal under this rule.</summary>
    bool QualifiedBy(decimal measuredValue);

    /// <summary>
    /// A human-readable justification of the decision for <paramref name="measuredValue"/> (e.g. "glicemia 268 ≥ 250
    /// mg/dL"), recorded on the animal's <see cref="InclusionDecision"/> so the cut is auditable.
    /// </summary>
    string Describe(decimal measuredValue, bool qualified);
}
