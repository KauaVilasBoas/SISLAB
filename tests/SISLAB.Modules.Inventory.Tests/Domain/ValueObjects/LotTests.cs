using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;

public sealed class LotTests
{
    [Fact]
    public void FromCode_creates_a_lot_from_a_non_empty_code()
    {
        Lot? lot = Lot.FromCode("ABC-123");

        Assert.NotNull(lot);
        Assert.Equal("ABC-123", lot!.Code);
    }

    [Fact]
    public void FromCode_trims_surrounding_whitespace()
    {
        Assert.Equal("XY9", Lot.FromCode("  XY9 ")!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromCode_returns_null_for_non_lot_controlled_items(string? code)
    {
        Assert.Null(Lot.FromCode(code));
    }

    [Fact]
    public void FromCode_rejects_codes_over_the_maximum_length()
    {
        string tooLong = new('L', 65);

        Assert.Throws<DomainException>(() => Lot.FromCode(tooLong));
    }

    [Fact]
    public void Lots_have_structural_equality()
    {
        Assert.Equal(Lot.FromCode("LOT1"), Lot.FromCode("LOT1"));
        Assert.NotEqual(Lot.FromCode("LOT1"), Lot.FromCode("LOT2"));
    }
}
