using SISLAB.Modules.Configuration.Domain.Units;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Tests.Domain.Units;

/// <summary>
/// Covers the per-tenant <see cref="Unit"/> aggregate (card [E12] #76): symbol/name normalization and the
/// deterministic seeding of the base catalogue that keeps tenant provisioning idempotent.
/// </summary>
public sealed class UnitTests
{
    [Fact]
    public void Create_trims_the_symbol_and_name()
    {
        Unit unit = Unit.Create("  mL  ", "  Mililitro  ");

        Assert.Equal("mL", unit.Symbol);
        Assert.Equal("Mililitro", unit.Name);
    }

    [Fact]
    public void Unit_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(Unit.Create("g", "Grama"));
    }

    [Theory]
    [InlineData(null, "Grama")]
    [InlineData("   ", "Grama")]
    [InlineData("g", null)]
    [InlineData("g", "   ")]
    public void Create_rejects_a_blank_symbol_or_name(string? symbol, string? name)
    {
        Assert.Throws<DomainException>(() => Unit.Create(symbol!, name!));
    }

    [Fact]
    public void Rename_changes_the_name_keeping_the_symbol()
    {
        Unit unit = Unit.Create("un", "Unidade");

        unit.Rename("  Peça  ");

        Assert.Equal("un", unit.Symbol);
        Assert.Equal("Peça", unit.Name);
    }

    [Fact]
    public void The_default_catalogue_covers_the_common_magnitudes()
    {
        IReadOnlyList<string> symbols = DefaultUnits.Catalogue.Select(entry => entry.Symbol).ToList();

        Assert.Contains("mL", symbols);
        Assert.Contains("g", symbols);
        Assert.Contains("un", symbols);
    }

    [Fact]
    public void The_default_catalogue_is_seeded_with_deterministic_ids_per_company()
    {
        Guid company = Guid.NewGuid();

        Assert.Equal(
            DefaultUnits.ForCompany(company).Select(unit => unit.Id),
            DefaultUnits.ForCompany(company).Select(unit => unit.Id));
    }

    [Fact]
    public void Seeded_unit_ids_differ_between_companies()
    {
        IEnumerable<Guid> a = DefaultUnits.ForCompany(Guid.NewGuid()).Select(u => u.Id);
        IEnumerable<Guid> b = DefaultUnits.ForCompany(Guid.NewGuid()).Select(u => u.Id);

        Assert.Empty(a.Intersect(b));
    }
}
