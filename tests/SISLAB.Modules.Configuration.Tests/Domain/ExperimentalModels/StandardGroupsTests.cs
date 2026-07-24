using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Domain.ExperimentalModels;

/// <summary>
/// Covers <see cref="StandardGroup"/> and <see cref="StandardGroups"/> (SISLAB-04): the "dose group carries a dose,
/// Naive/Control never do" invariant and the collection rules (at least one group, unique names, meaningful order).
/// The example curve (Naive + Controle + 3 g/kg + 0,6 g/kg) is test data, never a code constant.
/// </summary>
public sealed class StandardGroupsTests
{
    private static IEnumerable<StandardGroup> ExampleCurve() =>
    [
        StandardGroup.NonDosed("Naive", StandardGroupKind.Naive),
        StandardGroup.NonDosed("Controle", StandardGroupKind.Control),
        StandardGroup.Dosed("3 g/kg", 3m, "g/kg"),
        StandardGroup.Dosed("0,6 g/kg", 0.6m, "g/kg"),
    ];

    [Fact]
    public void NonDosed_group_carries_no_dose()
    {
        StandardGroup group = StandardGroup.NonDosed("Naive", StandardGroupKind.Naive);

        Assert.Equal(StandardGroupKind.Naive, group.Kind);
        Assert.Null(group.DoseAmount);
        Assert.Null(group.DoseUnit);
    }

    [Fact]
    public void Dosed_group_carries_the_dose_amount_and_unit()
    {
        StandardGroup group = StandardGroup.Dosed("3 g/kg", 3m, "  g/kg  ");

        Assert.Equal(StandardGroupKind.Dose, group.Kind);
        Assert.Equal(3m, group.DoseAmount);
        Assert.Equal("g/kg", group.DoseUnit);
    }

    [Fact]
    public void NonDosed_rejects_a_dose_kind()
    {
        Assert.Throws<DomainException>(() => StandardGroup.NonDosed("X", StandardGroupKind.Dose));
    }

    [Fact]
    public void Dosed_rejects_a_non_positive_dose()
    {
        Assert.Throws<DomainException>(() => StandardGroup.Dosed("X", 0m, "g/kg"));
    }

    [Fact]
    public void From_preserves_the_supplied_curve_order()
    {
        StandardGroups groups = StandardGroups.From(ExampleCurve());

        Assert.Collection(
            groups.Groups,
            group => Assert.Equal("Naive", group.Name),
            group => Assert.Equal("Controle", group.Name),
            group => Assert.Equal("3 g/kg", group.Name),
            group => Assert.Equal("0,6 g/kg", group.Name));
    }

    [Fact]
    public void From_rejects_an_empty_design()
    {
        Assert.Throws<DomainException>(() => StandardGroups.From([]));
    }

    [Fact]
    public void From_rejects_duplicate_group_names_case_insensitively()
    {
        Assert.Throws<DomainException>(() => StandardGroups.From(
        [
            StandardGroup.NonDosed("Controle", StandardGroupKind.Control),
            StandardGroup.NonDosed("controle", StandardGroupKind.Naive),
        ]));
    }
}
