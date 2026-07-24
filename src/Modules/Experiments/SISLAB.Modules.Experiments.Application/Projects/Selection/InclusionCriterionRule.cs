using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Domain.Projects;

namespace SISLAB.Modules.Experiments.Application.Projects.Selection;

/// <summary>
/// Application-layer adapter that presents a Configuration <see cref="InclusionCriterionDto"/> (SISLAB-02) as the
/// domain-local <see cref="IInclusionRule"/> the <see cref="Project"/> aggregate applies. This is the single seam
/// where the Configuration Contracts type is translated into the Experiments domain abstraction, so the aggregate
/// depends on no other module (module isolation, section 2) while still running the lab's cadastered comparison.
/// </summary>
/// <remarks>
/// The comparison itself is delegated to the DTO (<see cref="InclusionCriterionDto.Includes"/>), which mirrors the
/// domain's <c>InclusionThreshold</c> — the adapter never re-implements the operator semantics, it only shapes the
/// justification string recorded on the animal's decision.
/// </remarks>
internal sealed class InclusionCriterionRule : IInclusionRule
{
    private readonly InclusionCriterionDto _criterion;

    public InclusionCriterionRule(InclusionCriterionDto criterion) => _criterion = criterion;

    public string ParameterCode => _criterion.ParameterCode;

    public bool AppliesTo(string parameterCode) => _criterion.IsForParameter(parameterCode);

    public bool QualifiedBy(decimal measuredValue) => _criterion.Includes(measuredValue);

    public string Describe(decimal measuredValue, bool qualified)
        => $"{_criterion.ParameterCode} {measuredValue} {Symbol(_criterion.Operator)} " +
           $"{_criterion.Threshold} {_criterion.Unit} — {(qualified ? "incluído" : "excluído")}";

    private static string Symbol(string @operator) => @operator switch
    {
        "GreaterThanOrEqual" => "≥",
        "GreaterThan" => ">",
        "LessThanOrEqual" => "≤",
        "LessThan" => "<",
        "Equal" => "=",
        _ => @operator,
    };
}
