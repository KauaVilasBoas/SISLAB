using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.ValueObjects;

public sealed class EmailTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromValue_returns_null_for_a_blank_address(string? value)
        => Assert.Null(Email.FromValue(value));

    [Fact]
    public void FromValue_normalizes_to_trimmed_lowercase()
    {
        Email? email = Email.FromValue("  Vendas.BR@Merck.com  ");

        Assert.NotNull(email);
        Assert.Equal("vendas.br@merck.com", email!.Value);
    }

    [Theory]
    [InlineData("lab.barbosa@ufba.br")]
    [InlineData("vendas.br@merck.com")]
    public void FromValue_accepts_a_valid_institutional_address(string value)
        => Assert.NotNull(Email.FromValue(value));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@dot")]
    [InlineData("@no-local.com")]
    [InlineData("spaces in@address.com")]
    public void FromValue_rejects_an_invalid_address(string value)
        => Assert.Throws<DomainException>(() => Email.FromValue(value));

    [Fact]
    public void FromValue_rejects_an_address_that_is_too_long()
    {
        string tooLong = new string('a', 250) + "@x.com";

        Assert.Throws<DomainException>(() => Email.FromValue(tooLong));
    }

    [Fact]
    public void Two_emails_with_the_same_value_are_equal()
        => Assert.Equal(Email.FromValue("A@B.com"), Email.FromValue("a@b.com"));
}
