using SISLAB.Modules.Configuration.Domain.InclusionCriteria;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Domain.InclusionCriteria;

/// <summary>
/// Covers the inclusion criterion aggregate and its threshold value object (SISLAB-02): the comparison semantics per
/// operator and the "nothing lab-specific is a constant" rule (the parameter/threshold are always inputs).
/// </summary>
public sealed class InclusionCriterionTests
{
    [Theory]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 250, 250, true)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 250, 249, false)]
    [InlineData(ComparisonOperator.GreaterThan, 250, 251, true)]
    [InlineData(ComparisonOperator.GreaterThan, 250, 250, false)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 250, 250, true)]
    [InlineData(ComparisonOperator.LessThan, 250, 250, false)]
    [InlineData(ComparisonOperator.Equal, 250, 250, true)]
    public void Threshold_evaluates_each_operator(
        ComparisonOperator @operator,
        decimal threshold,
        decimal measured,
        bool expected)
    {
        InclusionThreshold rule = InclusionThreshold.Of(@operator, threshold);

        Assert.Equal(expected, rule.IsSatisfiedBy(measured));
    }

    [Fact]
    public void Threshold_is_compared_by_value()
    {
        Assert.Equal(
            InclusionThreshold.Of(ComparisonOperator.GreaterThanOrEqual, 250m),
            InclusionThreshold.Of(ComparisonOperator.GreaterThanOrEqual, 250m));
        Assert.NotEqual(
            InclusionThreshold.Of(ComparisonOperator.GreaterThanOrEqual, 250m),
            InclusionThreshold.Of(ComparisonOperator.GreaterThan, 250m));
    }

    [Fact]
    public void Create_builds_a_glicemia_criterion_from_inputs()
    {
        InclusionCriterion criterion = InclusionCriterion.Create(
            "glicemia", ComparisonOperator.GreaterThanOrEqual, 250m, "mg/dL");

        Assert.Equal("glicemia", criterion.ParameterCode);
        Assert.Equal("mg/dL", criterion.Unit);
        Assert.True(criterion.IsForParameter("GLICEMIA"));
        Assert.True(criterion.Includes(268m));
        Assert.False(criterion.Includes(214m));
    }

    [Fact]
    public void Create_trims_and_guards_a_present_parameter_and_unit()
    {
        InclusionCriterion criterion = InclusionCriterion.Create(
            "  glicemia  ", ComparisonOperator.GreaterThan, 100m, "  mg/dL  ");

        Assert.Equal("glicemia", criterion.ParameterCode);
        Assert.Equal("mg/dL", criterion.Unit);

        Assert.Throws<DomainException>(() =>
            InclusionCriterion.Create("  ", ComparisonOperator.GreaterThan, 100m, "mg/dL"));
    }

    [Fact]
    public void ChangeThreshold_replaces_the_comparison_keeping_the_parameter()
    {
        InclusionCriterion criterion = InclusionCriterion.Create(
            "peso", ComparisonOperator.GreaterThanOrEqual, 20m, "g");

        criterion.ChangeThreshold(ComparisonOperator.LessThan, 15m);

        Assert.Equal("peso", criterion.ParameterCode);
        Assert.True(criterion.Includes(10m));
        Assert.False(criterion.Includes(20m));
    }
}
