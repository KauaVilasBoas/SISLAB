using SISLAB.SharedKernel.Domain;

namespace SISLAB.SharedKernel.Tests.Domain;

/// <summary>
/// Testes de igualdade estrutural de <see cref="ValueObject"/>.
/// </summary>
public sealed class ValueObjectTests
{
    private sealed class Money : ValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    private sealed class Email : ValueObject
    {
        public string Address { get; }

        public Email(string address) => Address = address.ToLowerInvariant();

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Address;
        }
    }

    [Fact]
    public void TwoValueObjects_WithSameComponents_ShouldBeEqual()
    {
        var first = new Money(100.50m, "BRL");
        var second = new Money(100.50m, "BRL");

        Assert.Equal(first, second);
    }

    [Fact]
    public void TwoValueObjects_WithDifferentAmount_ShouldNotBeEqual()
    {
        var first = new Money(100m, "BRL");
        var second = new Money(200m, "BRL");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TwoValueObjects_WithDifferentCurrency_ShouldNotBeEqual()
    {
        var first = new Money(100m, "BRL");
        var second = new Money(100m, "USD");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TwoValueObjects_OfDifferentTypes_ShouldNotBeEqual()
    {
        var money = new Money(100m, "BRL");
        var email = new Email("test@test.com");

        Assert.NotEqual<ValueObject>(money, email);
    }

    [Fact]
    public void EqualityOperator_ShouldWork_ForValueObjects()
    {
        var first = new Money(50m, "USD");
        var second = new Money(50m, "USD");

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void GetHashCode_ShouldBeEqual_ForEqualValueObjects()
    {
        var first = new Money(99m, "BRL");
        var second = new Money(99m, "BRL");

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ShouldDiffer_ForDifferentValueObjects()
    {
        var first = new Money(1m, "BRL");
        var second = new Money(2m, "BRL");

        // Colisões de hash são possíveis mas improváveis com valores distintos
        Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ValueObject_WithNullComparison_ShouldReturnFalse()
    {
        var money = new Money(10m, "BRL");

        Assert.False(money.Equals(null));
    }

    [Fact]
    public void ValueObject_ComparedToItself_ShouldBeEqual()
    {
        var money = new Money(10m, "BRL");

        Assert.True(money.Equals(money));
    }
}
