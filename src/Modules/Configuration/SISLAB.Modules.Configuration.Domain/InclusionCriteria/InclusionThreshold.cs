using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Domain.InclusionCriteria;

/// <summary>
/// The predicate an <see cref="InclusionCriterion"/> tests a measured value against (SISLAB-02): a
/// <see cref="ComparisonOperator"/> paired with a numeric <see cref="Value"/> — e.g. "≥ 250". An immutable value
/// object with structural equality; it owns the "does this reading satisfy the criterion" rule so no aggregate or
/// handler re-implements the comparison.
/// </summary>
/// <remarks>
/// The threshold number (250 in the current lab) is data, never a code constant — it is supplied when the lab
/// cadasters its criterion. Modelling the operator + threshold as one value object keeps the comparison logic in a
/// single, unit-testable place and lets the aggregate stay thin.
/// </remarks>
public sealed class InclusionThreshold : ValueObject
{
    private InclusionThreshold(ComparisonOperator @operator, decimal value)
    {
        Operator = @operator;
        Value = value;
    }

    /// <summary>The comparison to apply between a measured value and <see cref="Value"/>.</summary>
    public ComparisonOperator Operator { get; }

    /// <summary>The numeric threshold the measured value is compared against.</summary>
    public decimal Value { get; }

    /// <summary>Builds a threshold, guarding a defined operator.</summary>
    public static InclusionThreshold Of(ComparisonOperator @operator, decimal value)
    {
        if (!Enum.IsDefined(@operator))
            throw new DomainException($"'{@operator}' is not a valid comparison operator.");

        return new InclusionThreshold(@operator, value);
    }

    /// <summary>Whether <paramref name="measuredValue"/> satisfies this threshold under its operator.</summary>
    public bool IsSatisfiedBy(decimal measuredValue) => Operator switch
    {
        ComparisonOperator.GreaterThanOrEqual => measuredValue >= Value,
        ComparisonOperator.GreaterThan => measuredValue > Value,
        ComparisonOperator.LessThanOrEqual => measuredValue <= Value,
        ComparisonOperator.LessThan => measuredValue < Value,
        ComparisonOperator.Equal => measuredValue == Value,
        _ => throw new DomainException($"Unhandled comparison operator '{Operator}'."),
    };

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Operator;
        yield return Value;
    }
}
