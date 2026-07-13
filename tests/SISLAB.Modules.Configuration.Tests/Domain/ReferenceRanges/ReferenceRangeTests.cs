using SISLAB.Modules.Configuration.Domain.ReferenceRanges;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Tests.Domain.ReferenceRanges;

/// <summary>
/// Covers the per-tenant <see cref="ReferenceRange"/> aggregate (card [E12] #76): text normalization for the
/// analyte/species natural key, the optional unit, and that the numeric invariant is delegated to
/// <see cref="RangeBounds"/> (so an inverted interval is rejected on create and on change).
/// </summary>
public sealed class ReferenceRangeTests
{
    private static ReferenceRange NewRange() =>
        ReferenceRange.Create("Hemoglobina", "Camundongo C57BL/6", 12m, 16m, "g/dL");

    [Fact]
    public void Create_normalizes_the_analyte_species_and_unit()
    {
        ReferenceRange range = ReferenceRange.Create("  Hemoglobina  ", "  C57BL/6  ", 12m, 16m, "  g/dL  ");

        Assert.Equal("Hemoglobina", range.Analyte);
        Assert.Equal("C57BL/6", range.Species);
        Assert.Equal("g/dL", range.Unit);
        Assert.True(range.Bounds.Contains(14m));
    }

    [Fact]
    public void ReferenceRange_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(NewRange());
    }

    [Fact]
    public void Create_leaves_a_blank_unit_null()
    {
        ReferenceRange range = ReferenceRange.Create("Glicose", "Rato Wistar", 70m, 110m, "   ");

        Assert.Null(range.Unit);
    }

    [Theory]
    [InlineData(null, "Rato")]
    [InlineData("   ", "Rato")]
    [InlineData("Analito", "   ")]
    public void Create_rejects_a_blank_analyte_or_species(string? analyte, string? species)
    {
        Assert.Throws<DomainException>(() => ReferenceRange.Create(analyte!, species!, 1m, 2m));
    }

    [Fact]
    public void Create_rejects_an_inverted_interval()
    {
        Assert.Throws<DomainException>(() =>
            ReferenceRange.Create("Hemoglobina", "Camundongo", 16m, 12m));
    }

    [Fact]
    public void ChangeBounds_replaces_the_interval()
    {
        ReferenceRange range = NewRange();

        range.ChangeBounds(10m, 14m);

        Assert.Equal(RangeBounds.Of(10m, 14m), range.Bounds);
    }

    [Fact]
    public void ChangeBounds_rejects_an_inverted_interval_and_keeps_the_previous_one()
    {
        ReferenceRange range = NewRange();

        Assert.Throws<DomainException>(() => range.ChangeBounds(16m, 12m));
        Assert.Equal(RangeBounds.Of(12m, 16m), range.Bounds);
    }

    [Fact]
    public void ChangeUnit_sets_and_clears_the_unit()
    {
        ReferenceRange range = NewRange();

        range.ChangeUnit("mg/dL");
        Assert.Equal("mg/dL", range.Unit);

        range.ChangeUnit("   ");
        Assert.Null(range.Unit);
    }
}
