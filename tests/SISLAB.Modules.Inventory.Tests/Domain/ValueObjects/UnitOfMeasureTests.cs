using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;

public sealed class UnitOfMeasureTests
{
    [Theory]
    [InlineData("g")]
    [InlineData("mg")]
    [InlineData("mL")]
    [InlineData("L")]
    [InlineData("unidade")]
    [InlineData("ampola")]
    [InlineData("caixa")]
    [InlineData("pacote")]
    [InlineData("kit")]
    public void FromSymbol_resolves_every_supported_unit(string symbol)
    {
        UnitOfMeasure unit = UnitOfMeasure.FromSymbol(symbol);

        Assert.Equal(symbol, unit.Symbol);
    }

    [Fact]
    public void FromSymbol_is_case_insensitive_and_trims_whitespace()
    {
        Assert.Equal(UnitOfMeasure.Milliliter, UnitOfMeasure.FromSymbol("  ML  "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromSymbol_rejects_blank_symbol(string? symbol)
    {
        Assert.Throws<DomainException>(() => UnitOfMeasure.FromSymbol(symbol!));
    }

    [Fact]
    public void FromSymbol_rejects_unknown_unit()
    {
        Assert.Throws<DomainException>(() => UnitOfMeasure.FromSymbol("gallon"));
    }

    [Theory]
    [InlineData("g", UnitDimension.Mass)]
    [InlineData("mg", UnitDimension.Mass)]
    [InlineData("mL", UnitDimension.Volume)]
    [InlineData("L", UnitDimension.Volume)]
    [InlineData("caixa", UnitDimension.Discrete)]
    public void Unit_carries_its_physical_dimension(string symbol, UnitDimension expected)
    {
        Assert.Equal(expected, UnitOfMeasure.FromSymbol(symbol).Dimension);
    }

    [Fact]
    public void Same_unit_is_compatible_with_itself()
    {
        Assert.True(UnitOfMeasure.Gram.IsCompatibleWith(UnitOfMeasure.Gram));
    }

    [Fact]
    public void Different_units_of_same_dimension_are_not_compatible_without_conversion()
    {
        Assert.False(UnitOfMeasure.Gram.IsCompatibleWith(UnitOfMeasure.Milligram));
    }

    [Fact]
    public void Units_of_different_dimensions_are_not_compatible()
    {
        Assert.False(UnitOfMeasure.Gram.IsCompatibleWith(UnitOfMeasure.Milliliter));
    }

    [Fact]
    public void Units_have_structural_equality()
    {
        Assert.Equal(UnitOfMeasure.Gram, UnitOfMeasure.FromSymbol("g"));
        Assert.True(UnitOfMeasure.Gram == UnitOfMeasure.FromSymbol("g"));
        Assert.NotEqual(UnitOfMeasure.Gram, UnitOfMeasure.Milligram);
    }
}
