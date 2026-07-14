using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Domain.ItemCategories;

/// <summary>
/// Covers the <see cref="CategoryAliases"/> value object (card [E12] #76): construction normalizes the raw
/// input (trim, drop blanks, case-insensitive de-dup, deterministic order), the invariants (length/count)
/// hold by design, and equality is structural and order-insensitive — so the alias set is a single conceptual
/// value, not a mutable list.
/// </summary>
public sealed class CategoryAliasesTests
{
    [Fact]
    public void None_is_empty()
    {
        Assert.Empty(CategoryAliases.None.Values);
    }

    [Fact]
    public void From_null_yields_the_empty_set()
    {
        Assert.Same(CategoryAliases.None, CategoryAliases.From(null));
    }

    [Fact]
    public void From_trims_drops_blanks_and_orders_case_insensitively()
    {
        CategoryAliases aliases = CategoryAliases.From(new[] { "  beta ", "", "   ", "Alpha" });

        Assert.Equal(new[] { "Alpha", "beta" }, aliases.Values);
    }

    [Fact]
    public void From_deduplicates_case_insensitively_keeping_the_first_casing()
    {
        CategoryAliases aliases = CategoryAliases.From(new[] { "Reagente", "REAGENTE", "reagente" });

        Assert.Equal(new[] { "Reagente" }, aliases.Values);
    }

    [Fact]
    public void From_rejects_an_alias_longer_than_the_maximum()
    {
        Assert.Throws<DomainException>(() => CategoryAliases.From(new[] { new string('x', 81) }));
    }

    [Fact]
    public void From_rejects_more_aliases_than_the_maximum()
    {
        IEnumerable<string> tooMany = Enumerable.Range(1, 21).Select(i => $"alias-{i}");

        Assert.Throws<DomainException>(() => CategoryAliases.From(tooMany));
    }

    [Theory]
    [InlineData("reagente", true)]
    [InlineData("  REAGENTE  ", true)]
    [InlineData("solvente", false)]
    [InlineData("   ", false)]
    public void Contains_matches_case_insensitively_and_trims(string candidate, bool expected)
    {
        CategoryAliases aliases = CategoryAliases.From(new[] { "Reagente", "RGT" });

        Assert.Equal(expected, aliases.Contains(candidate));
    }

    [Fact]
    public void Equality_is_structural_and_order_insensitive()
    {
        CategoryAliases a = CategoryAliases.From(new[] { "Alpha", "Beta" });
        CategoryAliases b = CategoryAliases.From(new[] { "beta", "ALPHA" });

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_alias_sets_are_not_equal()
    {
        Assert.NotEqual(
            CategoryAliases.From(new[] { "Alpha" }),
            CategoryAliases.From(new[] { "Beta" }));
    }
}
