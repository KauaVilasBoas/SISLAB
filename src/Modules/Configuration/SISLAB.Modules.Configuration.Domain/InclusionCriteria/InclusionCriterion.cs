using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.InclusionCriteria;

/// <summary>
/// A per-tenant animal-inclusion criterion (SISLAB-02): the configurable rule a laboratory cadasters to decide, from
/// a physiological reading, whether an animal enters the study — e.g. "glicemia ≥ 250 mg/dL" for a diabetes model.
/// It binds a physiological <see cref="ParameterCode"/> to an <see cref="InclusionThreshold"/> (operator + value) and
/// the <see cref="Unit"/> the threshold is expressed in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a distinct aggregate, not an extended <c>ReferenceRange</c>.</b> A reference range models a <i>healthy
/// interval</i> <c>(analyte, species) → [min, max]</c> and answers "is this result normal?". An inclusion criterion is
/// a directional <i>selection predicate</i> <c>(parameter, operator, threshold)</c> that answers "does this animal
/// qualify?". The operator (≥, &gt;, …) and the "qualify" semantics have no place on a healthy-interval type, so
/// conflating them would overload one aggregate with two intents. Keeping them apart follows the same rule the codebase
/// already applies (ReferenceRange vs ExperimentalModel): one aggregate, one reason to change.
/// </para>
/// <para>
/// <b>Applicability is the model's job.</b> Whether the criterion's parameter <i>applies</i> to a given study is
/// decided by the experimental model's applicable-parameters set (SISLAB-04), consulted by the Experiments module —
/// not here. A criterion is a standalone cadaster; a study whose model does not list the parameter simply never
/// evaluates it (the selection is non-blocking), so glicemia is ignored for a non-diabetic model without any coupling
/// between this aggregate and the model.
/// </para>
/// <para>
/// <b>Identity.</b> A criterion is identified within a tenant by its <see cref="ParameterCode"/> (a unique index
/// enforces one criterion per parameter per company). The comparison invariant lives entirely in
/// <see cref="InclusionThreshold"/>, so the aggregate stays a thin, rich orchestrator.
/// </para>
/// </remarks>
public sealed class InclusionCriterion : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxParameterCodeLength = 60;
    private const int MaxUnitLength = 30;

    // Parameterless constructor for EF Core materialization.
    private InclusionCriterion() : base(Guid.Empty) { }

    private InclusionCriterion(Guid id, string parameterCode, InclusionThreshold threshold, string unit) : base(id)
    {
        ParameterCode = parameterCode;
        Threshold = threshold;
        Unit = unit;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>The physiological parameter this criterion selects on (e.g. "glicemia"), unique per tenant.</summary>
    public string ParameterCode { get; private set; } = default!;

    /// <summary>The comparison + threshold the reading is tested against (e.g. ≥ 250).</summary>
    public InclusionThreshold Threshold { get; private set; } = default!;

    /// <summary>The unit the threshold is expressed in (e.g. "mg/dL").</summary>
    public string Unit { get; private set; } = default!;

    /// <summary>Creates an inclusion criterion for the active company from a validated threshold.</summary>
    public static InclusionCriterion Create(
        string parameterCode,
        ComparisonOperator @operator,
        decimal threshold,
        string unit)
        => new(
            Guid.NewGuid(),
            NormalizeText(parameterCode, MaxParameterCodeLength, nameof(parameterCode)),
            InclusionThreshold.Of(@operator, threshold),
            NormalizeText(unit, MaxUnitLength, nameof(unit)));

    /// <summary>Replaces the comparison/threshold, keeping the parameter identity.</summary>
    public void ChangeThreshold(ComparisonOperator @operator, decimal threshold)
        => Threshold = InclusionThreshold.Of(@operator, threshold);

    /// <summary>Sets the unit the threshold is expressed in.</summary>
    public void ChangeUnit(string unit) => Unit = NormalizeText(unit, MaxUnitLength, nameof(unit));

    /// <summary>Whether this criterion is for <paramref name="parameterCode"/> (case-insensitively).</summary>
    public bool IsForParameter(string parameterCode)
        => !string.IsNullOrWhiteSpace(parameterCode)
           && string.Equals(ParameterCode, parameterCode.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="measuredValue"/> qualifies the animal under this criterion.</summary>
    public bool Includes(decimal measuredValue) => Threshold.IsSatisfiedBy(measuredValue);

    private static string NormalizeText(string value, int maxLength, string parameterName)
    {
        Guard.AgainstNullOrWhiteSpace(value, parameterName);
        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
