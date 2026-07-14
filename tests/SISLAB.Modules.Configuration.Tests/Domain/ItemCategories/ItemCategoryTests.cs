using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Tests.Domain.ItemCategories;

/// <summary>
/// Covers the per-tenant <see cref="ItemCategory"/> aggregate (card [E12] #76) — the dynamic replacement for
/// the retired closed <c>StockItemCategory</c> enum: name normalization, the controlled flag, alias changes
/// and the deterministic seeding the enum→category backfill relies on.
/// </summary>
public sealed class ItemCategoryTests
{
    [Fact]
    public void Create_trims_the_name_and_defaults_to_not_controlled()
    {
        ItemCategory category = ItemCategory.Create("  Solvente  ");

        Assert.Equal("Solvente", category.Name);
        Assert.False(category.IsControlled);
        Assert.Empty(category.Aliases.Values);
    }

    [Fact]
    public void ItemCategory_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(ItemCategory.Create("Reagente"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string? blank)
    {
        Assert.Throws<DomainException>(() => ItemCategory.Create(blank!));
    }

    [Fact]
    public void Create_rejects_a_name_longer_than_the_maximum()
    {
        Assert.Throws<DomainException>(() => ItemCategory.Create(new string('x', 121)));
    }

    [Fact]
    public void Create_keeps_the_controlled_flag_and_aliases()
    {
        ItemCategory category = ItemCategory.Create(
            "Anestésico", aliases: new[] { "Anestesia", "ANEST" }, isControlled: true);

        Assert.True(category.IsControlled);
        Assert.Equal(new[] { "ANEST", "Anestesia" }, category.Aliases.Values);
    }

    [Fact]
    public void Rename_replaces_the_name_keeping_identity()
    {
        ItemCategory category = ItemCategory.Create("Solvente");
        Guid id = category.Id;

        category.Rename("  Solventes  ");

        Assert.Equal("Solventes", category.Name);
        Assert.Equal(id, category.Id);
    }

    [Fact]
    public void SetControlled_toggles_the_flag()
    {
        ItemCategory category = ItemCategory.Create("Opioide");

        category.SetControlled(true);
        Assert.True(category.IsControlled);

        category.SetControlled(false);
        Assert.False(category.IsControlled);
    }

    [Fact]
    public void ChangeAliases_replaces_the_alias_set()
    {
        ItemCategory category = ItemCategory.Create("Reagente", aliases: new[] { "old" });

        category.ChangeAliases(new[] { "RGT", "Reagentes" });

        Assert.Equal(new[] { "Reagentes", "RGT" }, category.Aliases.Values);
    }

    [Fact]
    public void The_default_catalogue_has_the_nine_legacy_categories()
    {
        Assert.Equal(9, DefaultItemCategories.Catalogue.Count);
        Assert.Contains(DefaultItemCategories.Catalogue, entry => entry.Code == "Solvent");
        Assert.Contains(
            DefaultItemCategories.Catalogue,
            entry => entry.Code == "ControlledAnesthetic" && entry.IsControlled);
    }

    [Fact]
    public void The_default_catalogue_is_seeded_with_deterministic_ids_per_company()
    {
        Guid company = Guid.NewGuid();

        IReadOnlyList<ItemCategory> first = DefaultItemCategories.ForCompany(company);
        IReadOnlyList<ItemCategory> second = DefaultItemCategories.ForCompany(company);

        // Same (company, code) pair yields the same id on every run — what makes the seed/backfill idempotent.
        Assert.Equal(
            first.Select(category => category.Id),
            second.Select(category => category.Id));
    }

    [Fact]
    public void Seeded_category_ids_differ_between_companies()
    {
        IReadOnlyList<ItemCategory> companyA = DefaultItemCategories.ForCompany(Guid.NewGuid());
        IReadOnlyList<ItemCategory> companyB = DefaultItemCategories.ForCompany(Guid.NewGuid());

        Assert.Empty(companyA.Select(c => c.Id).Intersect(companyB.Select(c => c.Id)));
    }
}
