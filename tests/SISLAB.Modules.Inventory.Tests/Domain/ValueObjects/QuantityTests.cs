using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;

public sealed class QuantityTests
{
    [Fact]
    public void Of_creates_a_quantity_with_value_and_unit()
    {
        Quantity quantity = Quantity.Of(5m, UnitOfMeasure.Milliliter);

        Assert.Equal(5m, quantity.Value);
        Assert.Equal(UnitOfMeasure.Milliliter, quantity.Unit);
    }

    [Fact]
    public void Of_rejects_negative_values()
    {
        Assert.Throws<DomainException>(() => Quantity.Of(-1m, UnitOfMeasure.Gram));
    }

    [Fact]
    public void Zero_is_allowed_and_reported()
    {
        Quantity zero = Quantity.Zero(UnitOfMeasure.Gram);

        Assert.True(zero.IsZero);
        Assert.Equal(0m, zero.Value);
    }

    [Fact]
    public void Add_sums_quantities_of_the_same_unit()
    {
        Quantity result = Quantity.Of(2.5m, UnitOfMeasure.Gram).Add(Quantity.Of(1.5m, UnitOfMeasure.Gram));

        Assert.Equal(Quantity.Of(4m, UnitOfMeasure.Gram), result);
    }

    [Fact]
    public void Subtract_reduces_quantities_of_the_same_unit()
    {
        Quantity result = Quantity.Of(10m, UnitOfMeasure.Milliliter).Subtract(Quantity.Of(4m, UnitOfMeasure.Milliliter));

        Assert.Equal(Quantity.Of(6m, UnitOfMeasure.Milliliter), result);
    }

    [Fact]
    public void Subtract_rejects_results_below_zero()
    {
        Assert.Throws<DomainException>(
            () => Quantity.Of(1m, UnitOfMeasure.Gram).Subtract(Quantity.Of(2m, UnitOfMeasure.Gram)));
    }

    [Fact]
    public void Add_blocks_incompatible_units()
    {
        Assert.Throws<DomainException>(
            () => Quantity.Of(1m, UnitOfMeasure.Gram).Add(Quantity.Of(1m, UnitOfMeasure.Milliliter)));
    }

    [Fact]
    public void Add_blocks_same_dimension_but_different_units()
    {
        Assert.Throws<DomainException>(
            () => Quantity.Of(1m, UnitOfMeasure.Gram).Add(Quantity.Of(1000m, UnitOfMeasure.Milligram)));
    }

    [Fact]
    public void Comparisons_respect_the_unit()
    {
        Quantity ten = Quantity.Of(10m, UnitOfMeasure.Unit);
        Quantity three = Quantity.Of(3m, UnitOfMeasure.Unit);

        Assert.True(ten.IsGreaterThanOrEqualTo(three));
        Assert.True(three.IsLessThan(ten));
    }

    [Fact]
    public void Comparisons_block_incompatible_units()
    {
        Assert.Throws<DomainException>(
            () => Quantity.Of(1m, UnitOfMeasure.Gram).IsLessThan(Quantity.Of(1m, UnitOfMeasure.Liter)));
    }

    [Fact]
    public void Quantities_have_structural_equality()
    {
        Assert.Equal(Quantity.Of(3m, UnitOfMeasure.Gram), Quantity.Of(3m, UnitOfMeasure.Gram));
        Assert.NotEqual(Quantity.Of(3m, UnitOfMeasure.Gram), Quantity.Of(3m, UnitOfMeasure.Milligram));
    }

    [Fact]
    public void Operations_do_not_mutate_the_source()
    {
        Quantity original = Quantity.Of(5m, UnitOfMeasure.Gram);
        original.Add(Quantity.Of(1m, UnitOfMeasure.Gram));

        Assert.Equal(5m, original.Value);
    }
}
