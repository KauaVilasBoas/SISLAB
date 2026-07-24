using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Domain.ExperimentalModels;

/// <summary>
/// Covers <see cref="StandardTimepoints"/> (ordered, at-least-one) and <see cref="ApplicableParameters"/>
/// (order-insensitive set, may be empty, membership test) — SISLAB-04. The example labels/codes are test data.
/// </summary>
public sealed class StandardTimepointsAndParametersTests
{
    [Fact]
    public void Timepoints_preserve_reading_order_and_drop_blanks_and_duplicates()
    {
        StandardTimepoints timepoints = StandardTimepoints.From(
            ["  Basal  ", "Pós-indução", "", "7 dias", "Basal"]);

        Assert.Collection(
            timepoints.Labels,
            label => Assert.Equal("Basal", label),
            label => Assert.Equal("Pós-indução", label),
            label => Assert.Equal("7 dias", label));
    }

    [Fact]
    public void Timepoints_require_at_least_one_label()
    {
        Assert.Throws<DomainException>(() => StandardTimepoints.From(["   ", ""]));
        Assert.Throws<DomainException>(() => StandardTimepoints.From(null));
    }

    [Fact]
    public void Parameters_normalize_to_a_stable_order_and_dedupe()
    {
        ApplicableParameters parameters = ApplicableParameters.From(["rotarod", "glicemia", "Glicemia", "peso"]);

        Assert.Equal(["glicemia", "peso", "rotarod"], parameters.Codes);
    }

    [Fact]
    public void Parameters_may_be_empty()
    {
        Assert.Empty(ApplicableParameters.From(null).Codes);
        Assert.Empty(ApplicableParameters.None.Codes);
    }

    [Fact]
    public void Parameters_applies_is_case_insensitive()
    {
        ApplicableParameters parameters = ApplicableParameters.From(["glicemia", "peso"]);

        Assert.True(parameters.Applies("Glicemia"));
        Assert.False(parameters.Applies("rotarod"));
    }
}
